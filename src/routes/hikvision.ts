import express from 'express';

import { db } from '../db/client';


const router = express.Router();


router.post('/hikvision', express.json(), async (req, res) => {

    const event = req.body;


    try {

        const jobNo = event.sJobNo;

        const status = event.AttendanceStatus;

        const timestamp = event.eventTime || new Date().toISOString();


        await db.query(

            'INSERT INTO asistencia (id_empleado, entrada_salida, fecha_hora) VALUES ($1, $2, $3)',

            [jobNo, status, timestamp]

        );


        res.status(200).json({ message: 'Evento registrado' });

    } catch (err) {

        console.error(err);

        res.status(500).json({ error: 'Error al guardar evento' });

    }

});


export default router;