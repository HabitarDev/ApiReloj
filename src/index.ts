import express from 'express';

import dotenv from 'dotenv';

import hikvisionRoute from './routes/hikvision';


dotenv.config();

const app = express();


app.use('/eventos', hikvisionRoute);


const port = process.env.PORT || 3000;

app.listen(port, () => {

    console.log(`Servidor corriendo en puerto ${port}`);

});
