#!/usr/bin/env bash

set -Eeuo pipefail

readonly CONTAINER_NAME="${CENTRASA_CONTAINER_NAME:-centrasa}"
readonly BACKUP_SCRIPT="${CENTRASA_BACKUP_SCRIPT:-/usr/local/sbin/centrasa-backup}"
readonly STATE_ROOT="${CENTRASA_STATE_ROOT:-/var/lib/centrasa}"

if [[ "$(id -u)" -ne 0 ]]; then
    echo "Execute este script como root." >&2
    exit 1
fi

if [[ $# -ne 1 || ! "$1" =~ ^[0-9]+\.[0-9]+\.[0-9]+([.-][A-Za-z0-9._-]+)?$ ]]; then
    echo "Uso: $0 <versão semver, por exemplo 1.0.1>" >&2
    exit 2
fi

readonly NEW_TAG="$1"
readonly NEW_IMAGE="centrasa:${NEW_TAG}"

if ! docker image inspect "${NEW_IMAGE}" >/dev/null 2>&1; then
    echo "Imagem local não encontrada: ${NEW_IMAGE}" >&2
    exit 1
fi

if [[ ! -x "${BACKUP_SCRIPT}" ]]; then
    echo "Script de backup não executável: ${BACKUP_SCRIPT}" >&2
    exit 1
fi

readonly CURRENT_IMAGE="$(docker inspect --format '{{.Config.Image}}' "${CONTAINER_NAME}")"
if [[ "${CURRENT_IMAGE}" == "${NEW_IMAGE}" ]]; then
    echo "O container já usa ${NEW_IMAGE}." >&2
    exit 1
fi

"${BACKUP_SCRIPT}"

umask 077
install -d -m 0700 "${STATE_ROOT}"
printf '%s\n' "${CURRENT_IMAGE}" > "${STATE_ROOT}/previous-image"
printf '%s\n' "${NEW_IMAGE}" > "${STATE_ROOT}/pending-image"

echo "Preparação concluída. No Portainer, defina CENTRASA_IMAGE_TAG=${NEW_TAG} e redeploye a stack."
echo "Imagem anterior registrada para rollback: ${CURRENT_IMAGE}"
