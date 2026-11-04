import { getHealth } from './health.controller.js';

export const handleHealthRoutes = (req, res, pathname) => {
  if (req.method === 'GET') {
    return getHealth(req, res);
  }
  res.json({ error: 'Method Not Allowed' }, 405);
};
