#!/usr/bin/env bash

set -Eeuo pipefail

readonly CONTAINER_NAME="${CENTRASA_CONTAINER_NAME:-centrasa}"
readonly VOLUME_NAME="${CENTRASA_VOLUME_NAME:-centrasa_data}"
readonly BACKUP_ROOT="${CENTRASA_BACKUP_ROOT:-/var/backups/centrasa}"
readonly LOCK_FILE="${CENTRASA_LOCK_FILE:-/run/lock/centrasa-maintenance.lock}"
readonly TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"

if [[ "$(id -u)" -ne 0 ]]; then
    echo "Execute este script como root." >&2
    exit 1
fi

if [[ $# -ne 1 ]]; then
    echo "Uso: $0 <snapshot centrasa-AAAAMMDDTHHMMSSZ>" >&2
    exit 2
fi

for command_name in docker flock realpath sha256sum; do
    if ! command -v "${command_name}" >/dev/null 2>&1; then
        echo "Comando obrigatório não encontrado: ${command_name}" >&2
        exit 1
    fi
done

exec 9>"${LOCK_FILE}"
if ! flock --nonblock 9; then
    echo "Outra manutenção do CentraSA já está em execução." >&2
    exit 1
fi

if ! docker inspect "${CONTAINER_NAME}" >/dev/null 2>&1; then
    echo "Container não encontrado: ${CONTAINER_NAME}" >&2
    exit 1
fi

if ! docker volume inspect "${VOLUME_NAME}" >/dev/null 2>&1; then
    echo "Volume não encontrado: ${VOLUME_NAME}" >&2
    exit 1
fi

readonly BACKUP_ROOT_REAL="$(realpath -e "${BACKUP_ROOT}")"
snapshot_input="$1"
if [[ "${snapshot_input}" != /* ]]; then
    snapshot_input="${BACKUP_ROOT}/${snapshot_input}"
fi
readonly SNAPSHOT_REAL="$(realpath -e "${snapshot_input}")"

case "${SNAPSHOT_REAL}" in
    "${BACKUP_ROOT_REAL}"/centrasa-[0-9]*T[0-9]*Z) ;;
    *) echo "Snapshot recusado: use um diretório centrasa-* dentro de ${BACKUP_ROOT_REAL}." >&2; exit 1 ;;
esac

if [[ ! -s "${SNAPSHOT_REAL}/data/Data/centrasa.db" ]]; then
    echo "Snapshot inválido: banco ausente ou vazio." >&2
    exit 1
fi

if [[ ! -f "${SNAPSHOT_REAL}/SHA256SUMS" ]]; then
    echo "Snapshot inválido: SHA256SUMS ausente." >&2
    exit 1
fi

(
    cd "${SNAPSHOT_REAL}"
    sha256sum --check SHA256SUMS
)

readonly IMAGE_NAME="$(docker inspect --format '{{.Config.Image}}' "${CONTAINER_NAME}")"
readonly PREVENTIVE_DIR="${BACKUP_ROOT_REAL}/pre-restore-${TIMESTAMP}"
umask 077

was_running=false
if [[ "$(docker inspect --format '{{.State.Running}}' "${CONTAINER_NAME}")" == "true" ]]; then
    was_running=true
    docker stop --time 30 "${CONTAINER_NAME}" >/dev/null
fi

restart_after_early_failure() {
    local exit_code=$?
    trap - EXIT
    if [[ "${exit_code}" -ne 0 && "${was_running}" == "true" ]]; then
        docker start "${CONTAINER_NAME}" >/dev/null || true
    fi
    exit "${exit_code}"
}
trap restart_after_early_failure EXIT

mkdir "${PREVENTIVE_DIR}"
docker cp "${CONTAINER_NAME}:/data/." "${PREVENTIVE_DIR}/data"
if [[ ! -s "${PREVENTIVE_DIR}/data/Data/centrasa.db" ]]; then
    echo "Cópia preventiva inválida: banco atual ausente ou vazio." >&2
    exit 1
fi
(
    cd "${PREVENTIVE_DIR}"
    find data -type f -print0 | sort -z | xargs -0 --no-run-if-empty sha256sum > SHA256SUMS
)

replace_volume_data() {
    local source_dir="$1"

    docker run --rm \
        --user 0:0 \
        --entrypoint /bin/sh \
        --mount "type=volume,src=${VOLUME_NAME},dst=/data" \
        "${IMAGE_NAME}" \
        -c 'find /data -mindepth 1 -maxdepth 1 -exec rm -rf -- {} +'

    docker cp "${source_dir}/data/." "${CONTAINER_NAME}:/data/"

    docker run --rm \
        --user 0:0 \
        --entrypoint /bin/sh \
        --mount "type=volume,src=${VOLUME_NAME},dst=/data" \
        "${IMAGE_NAME}" \
        -c 'test -n "$APP_UID" && chown -R "$APP_UID:$APP_UID" /data'
}

rollback_on_error() {
    local exit_code=$?
    trap - ERR
    set +e
    echo "Restauração falhou; repondo a cópia preventiva." >&2
    if [[ "$(docker inspect --format '{{.State.Running}}' "${CONTAINER_NAME}" 2>/dev/null)" == "true" ]]; then
        docker stop --time 30 "${CONTAINER_NAME}" >/dev/null
    fi
    replace_volume_data "${PREVENTIVE_DIR}"
    if [[ "${was_running}" == "true" ]]; then
        docker start "${CONTAINER_NAME}" >/dev/null
    fi
    exit "${exit_code}"
}
trap - EXIT
trap rollback_on_error ERR

replace_volume_data "${SNAPSHOT_REAL}"
docker start "${CONTAINER_NAME}" >/dev/null

healthy=false
for _ in {1..45}; do
    health_status="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' "${CONTAINER_NAME}")"
    if [[ "${health_status}" == "healthy" ]]; then
        healthy=true
        break
    fi
    if [[ "${health_status}" == "unhealthy" ]]; then
        break
    fi
    sleep 2
done

if [[ "${healthy}" != "true" ]]; then
    echo "Container não ficou saudável após a restauração." >&2
    false
fi

trap - ERR
echo "Restauração concluída a partir de ${SNAPSHOT_REAL}."
echo "Cópia preventiva mantida em ${PREVENTIVE_DIR}."
