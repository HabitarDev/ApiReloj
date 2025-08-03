import { Router } from 'express';
import { db } from '../db/client';

const router = Router();

router.post('/hikvision', async (req, res) => {
    try {
        const { eventTime, employeeNoString, attendanceStatus } = req.body;

        if (!eventTime || !employeeNoString || !attendanceStatus) {
            return res.status(400).json({ error: 'Faltan datos necesarios.' });
        }

        await db.query(
            'INSERT INTO asistencia (employee_id, nombre, fecha_hora, tipo) VALUES ($1, $2, $3, $4)',
            [employeeNoString, '', eventTime, attendanceStatus]
        );

        res.status(200).json({ message: 'Datos guardados correctamente' });
    } catch (error) {
        console.error('Error al guardar en la base de datos:', error);
        res.status(500).json({ error: 'Error del servidor' });
    }
});

export default router;
