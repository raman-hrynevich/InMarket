#!/bin/sh
set -eu

base="${UNLEASH_URL%/}"
flag="use-keycloak-auth"

echo "waiting for the Unleash admin API"
i=0
authed=0
while [ "$i" -lt 40 ]; do
  if curl -fsS -o /dev/null -H "Authorization: ${UNLEASH_ADMIN_TOKEN}" "${base}/api/admin/projects"; then
    authed=1
    break
  fi
  i=$((i + 1))
  sleep 3
done

auth_header="Authorization: ${UNLEASH_ADMIN_TOKEN}"
cookie_args=""

if [ "$authed" -ne 1 ]; then
  echo "admin token was rejected, logging in as ${UNLEASH_ADMIN_USERNAME}"
  i=0
  while [ "$i" -lt 20 ]; do
    if curl -fsS -c /tmp/unleash.cookies -H "Content-Type: application/json" \
      -d "{\"username\":\"${UNLEASH_ADMIN_USERNAME}\",\"password\":\"${UNLEASH_ADMIN_PASSWORD}\"}" \
      "${base}/auth/simple/login"; then
      cookie_args="-b /tmp/unleash.cookies"
      auth_header=""
      break
    fi
    i=$((i + 1))
    sleep 3
  done
  if [ -z "$cookie_args" ]; then
    echo "could not authenticate to Unleash"
    exit 1
  fi
fi

curl_auth() {
  if [ -n "$cookie_args" ]; then
    curl "$@" -b /tmp/unleash.cookies
  else
    curl "$@" -H "$auth_header"
  fi
}

create_code=$(curl_auth -sS -o /tmp/unleash-create.json -w "%{http_code}" \
  -X POST -H "Content-Type: application/json" \
  -d "{\"name\":\"${flag}\",\"description\":\"Sign in with Keycloak in the local Docker stack\",\"type\":\"release\"}" \
  "${base}/api/admin/projects/default/features")

case "$create_code" in
  200|201|409) echo "feature ${flag}: ${create_code}" ;;
  *)
    echo "create feature failed: ${create_code}"
    cat /tmp/unleash-create.json
    exit 1
    ;;
esac

strategies=$(curl_auth -fsS \
  "${base}/api/admin/projects/default/features/${flag}/environments/development/strategies" || true)

if ! printf '%s' "$strategies" | grep -q 'flexibleRollout'; then
  strategy_code=$(curl_auth -sS -o /tmp/unleash-strategy.json -w "%{http_code}" \
    -X POST -H "Content-Type: application/json" \
    -d '{"name":"flexibleRollout","title":"All users","constraints":[],"parameters":{"rollout":"100","stickiness":"default","groupId":"use-keycloak-auth"}}' \
    "${base}/api/admin/projects/default/features/${flag}/environments/development/strategies")
  case "$strategy_code" in
    200|201|409) echo "strategy: ${strategy_code}" ;;
    *)
      echo "strategy failed: ${strategy_code}"
      cat /tmp/unleash-strategy.json
      exit 1
      ;;
  esac
else
  echo "strategy already present"
fi

enable_code=$(curl_auth -sS -o /tmp/unleash-on.json -w "%{http_code}" \
  -X POST -H "Content-Type: application/json" -d '{}' \
  "${base}/api/admin/projects/default/features/${flag}/environments/development/on")

case "$enable_code" in
  200|202|204|409) echo "flag ${flag} enabled: ${enable_code}" ;;
  *)
    echo "enable failed: ${enable_code}"
    cat /tmp/unleash-on.json
    exit 1
    ;;
esac
