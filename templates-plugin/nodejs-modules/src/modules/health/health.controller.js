import { config } from '../../config/app.js';

export const getHealth = (req, res) => {
  res.json({
    status: 'healthy',
    project: config.projectName,
    uptime: process.uptime(),
    timestamp: new Date().toISOString()
  });
};

