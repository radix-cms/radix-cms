import express, { Request, Response } from 'express';
import dotenv from 'dotenv';
import path from 'node:path';

dotenv.config({ path: path.resolve(__dirname, '..', '.env') });

const app = express();
const port = Number(process.env.SITE_PORT);

if (!Number.isInteger(port) || port < 1 || port > 65535)
	throw new Error('SITE_PORT in .env must be an integer between 1 and 65535.');

app.get('/', (req: Request, res: Response) => {
  res.send('Salve mundi.');
});

app.listen(port, () => {
  console.log(`Server is running at http://localhost:${port}`);
});