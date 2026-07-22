#!/usr/bin/env bash

set -Eeuo pipefail

readonly CONTAINER_NAME="${CENTRASA_CONTAINER_NAME:-centrasa}"
readonly BACKUP_SCRIPT="${CENTRASA_BACKUP_SCRIPT:-/usr/local/sbin/centrasa-backup}"
readonly STATE_ROOT="${CENTRASA_STATE_ROOT:-/var/lib/centrasa}"
readonly PREVIOUS_IMAGE_FILE="${STATE_ROOT}/previous-image"

if [[ "$(id -u)" -ne 0 ]]; then
    echo "Execute este script como root." >&2
    exit 1
fi

if [[ ! -s "${PREVIOUS_IMAGE_FILE}" ]]; then
    echo "Nenhuma imagem anterior registrada em ${PREVIOUS_IMAGE_FILE}." >&2
    exit 1
fi

readonly PREVIOUS_IMAGE="$(<"${PREVIOUS_IMAGE_FILE}")"
readonly PREVIOUS_TAG="${PREVIOUS_IMAGE#centrasa:}"
if [[ "${PREVIOUS_IMAGE}" == "${PREVIOUS_TAG}" || ! "${PREVIOUS_TAG}" =~ ^[0-9]+\.[0-9]+\.[0-9]+([.-][A-Za-z0-9._-]+)?$ ]]; then
    echo "Referência de imagem anterior inválida: ${PREVIOUS_IMAGE}" >&2
    exit 1
fi

if ! docker image inspect "${PREVIOUS_IMAGE}" >/dev/null 2>&1; then
    echo "Imagem anterior não está disponível localmente: ${PREVIOUS_IMAGE}" >&2
    exit 1
fi

readonly CURRENT_IMAGE="$(docker inspect --format '{{.Config.Image}}' "${CONTAINER_NAME}")"
if [[ "${CURRENT_IMAGE}" == "${PREVIOUS_IMAGE}" ]]; then
    echo "O container já usa ${PREVIOUS_IMAGE}." >&2
    exit 1
fi

if [[ ! -x "${BACKUP_SCRIPT}" ]]; then
    echo "Script de backup não executável: ${BACKUP_SCRIPT}" >&2
    exit 1
fi

"${BACKUP_SCRIPT}"

echo "Snapshot pré-rollback concluído."
echo "No Portainer, defina CENTRASA_IMAGE_TAG=${PREVIOUS_TAG} e redeploye a stack."
echo "Se a versão nova aplicou migration incompatível, restaure também o snapshot anterior à atualização."
