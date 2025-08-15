module.exports = {
    apps: [
        {
            name: "hikvision-poller",
            cwd: "/home/ubuntu/ApiReloj",              // raíz del repo
            script: "dist/poller/index.js",            // entrada compilada
            interpreter: "/usr/bin/node",
            instances: 1,
            exec_mode: "fork",
            autorestart: true,
            watch: false,
            max_memory_restart: "300M",

            // LOGS de PM2 (independientes de pino). Útiles para ver stdout/stderr.
            out_file: "/home/ubuntu/ApiReloj/pm2-logs/out.log",
            error_file: "/home/ubuntu/ApiReloj/pm2-logs/err.log",
            merge_logs: true,

            // Variables de entorno en PRODUCCIÓN (forzamos modo inseguro)
            env: {
                NODE_ENV: "production",
                RELOJ_TLS_INSECURE: "1"    // <- clave para aceptar certificado vencido/autofirmado
            }
        }
    ]
};
