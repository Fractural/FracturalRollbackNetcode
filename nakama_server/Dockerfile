FROM heroiclabs/nakama-pluginbuilder:3.15.0 AS builder

ENV GO111MODULE on
ENV CGO_ENABLED 1

WORKDIR /backend
COPY go.mod .
COPY *.go .
COPY vendor/ vendor/

RUN	go build --trimpath --mod=vendor --buildmode=plugin -o ./backend.so

FROM heroiclabs/nakama:3.15.0

# Copy our go module into nakama
COPY --from=builder /backend/backend.so /nakama/data/modules/
COPY local.yml /nakama/data