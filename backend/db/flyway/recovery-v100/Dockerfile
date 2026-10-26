FROM flyway/flyway:10-alpine

USER root
RUN apk add --no-cache jq
COPY repair-v100-entrypoint.sh /usr/local/bin/repair-v100-entrypoint.sh
RUN chmod 0755 /usr/local/bin/repair-v100-entrypoint.sh

ENTRYPOINT ["/usr/local/bin/repair-v100-entrypoint.sh"]
CMD ["-?"]
