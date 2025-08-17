module.exports = {
    apps: [
        {
            name: "hikvision-poller",
            cwd: "/home/ubuntu/ApiReloj",
            script: "dist/poller/index.js",
            interpreter: "/usr/bin/node",
            instances: 1,
            exec_mode: "fork",
            autorestart: true,
            watch: false,
            max_memory_restart: "300M",

            // PM2 logs (stdout/err). Los logs funcionales siguen en pino -> poller.log
            out_file: "/home/ubuntu/ApiReloj/pm2-logs/out.log",
            error_file: "/home/ubuntu/ApiReloj/pm2-logs/err.log",
            merge_logs: true,

            // Cargar el .env
            env_file: "/home/ubuntu/ApiReloj/.env",

            // Variables extra/override para producción
            env: {
                NODE_ENV: "production",
                LOG_TZ_OFFSET: "-03:00",
                LOG_LEVEL: "info",          // usa "debug" si querés más verbosidad
                // Forzá modo inseguro mientras testeás
                RELOJ_TLS_INSECURE: "1",
                // Opcional: forzar dónde escribir logs de pino
                // LOG_DIR: "/home/ubuntu/ApiReloj/logs",
                // Opcional: para ver el body que se envía al reloj
                 LOG_INCLUDE_BODY: "1"
            }
        }
    ]
};
