#!/usr/bin/env bash

set -Eeuo pipefail

readonly CONTAINER_NAME="${CENTRASA_CONTAINER_NAME:-centrasa}"
readonly BACKUP_ROOT="${CENTRASA_BACKUP_ROOT:-/var/backups/centrasa}"
readonly RETENTION_COUNT="${CENTRASA_BACKUP_RETENTION:-7}"
readonly LOCK_FILE="${CENTRASA_LOCK_FILE:-/run/lock/centrasa-maintenance.lock}"
readonly TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
readonly SNAPSHOT_DIR="${BACKUP_ROOT}/centrasa-${TIMESTAMP}"
readonly WORK_DIR="${BACKUP_ROOT}/.centrasa-${TIMESTAMP}.incomplete"

if [[ "$(id -u)" -ne 0 ]]; then
    echo "Execute este script como root." >&2
    exit 1
fi

if ! [[ "${RETENTION_COUNT}" =~ ^[1-9][0-9]*$ ]]; then
    echo "CENTRASA_BACKUP_RETENTION deve ser um inteiro maior que zero." >&2
    exit 1
fi

for command_name in docker find flock sha256sum sort; do
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

umask 077
install -d -m 0700 "${BACKUP_ROOT}"

was_running=false
if [[ "$(docker inspect --format '{{.State.Running}}' "${CONTAINER_NAME}")" == "true" ]]; then
    was_running=true
fi

cleanup() {
    local exit_code=$?
    trap - EXIT INT TERM

    if [[ "${was_running}" == "true" ]]; then
        docker start "${CONTAINER_NAME}" >/dev/null || true
    fi

    if [[ -d "${WORK_DIR}" ]]; then
        rm -rf -- "${WORK_DIR}"
    fi

    exit "${exit_code}"
}
trap cleanup EXIT INT TERM

if [[ "${was_running}" == "true" ]]; then
    docker stop --time 30 "${CONTAINER_NAME}" >/dev/null
fi

mkdir "${WORK_DIR}"
docker cp "${CONTAINER_NAME}:/data/." "${WORK_DIR}/data"

readonly DATABASE_COPY="${WORK_DIR}/data/Data/centrasa.db"
if [[ ! -s "${DATABASE_COPY}" ]]; then
    echo "Backup inválido: Data/centrasa.db não existe ou está vazio." >&2
    exit 1
fi

(
    cd "${WORK_DIR}"
    find data -type f -print0 | sort -z | xargs -0 --no-run-if-empty sha256sum > SHA256SUMS
)

mv "${WORK_DIR}" "${SNAPSHOT_DIR}"

if [[ "${was_running}" == "true" ]]; then
    docker start "${CONTAINER_NAME}" >/dev/null
    was_running=false
fi

mapfile -t snapshots < <(
    find "${BACKUP_ROOT}" \
        -mindepth 1 \
        -maxdepth 1 \
        -type d \
        -name 'centrasa-[0-9]*T[0-9]*Z' \
        -printf '%f\n' \
        | sort --reverse
)

for ((index = RETENTION_COUNT; index < ${#snapshots[@]}; index++)); do
    candidate="${BACKUP_ROOT}/${snapshots[index]}"
    case "${candidate}" in
        "${BACKUP_ROOT}"/centrasa-*) rm -rf -- "${candidate}" ;;
        *) echo "Retenção recusou caminho inesperado: ${candidate}" >&2; exit 1 ;;
    esac
done

trap - EXIT INT TERM
echo "Backup concluído: ${SNAPSHOT_DIR}"
