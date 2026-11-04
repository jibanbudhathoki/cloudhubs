import http from 'node:http';
import { config } from './config/app.js';
import { handleHealthRoutes } from './modules/health/health.routes.js';
import { handleUserRoutes } from './modules/users/user.routes.js';

const server = http.createServer(async (req, res) => {
  // CORS Headers
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Origin, X-Requested-With, Content-Type, Accept');

  if (req.method === 'OPTIONS') {
    res.writeHead(204);
    res.end();
    return;
  }

  const url = new URL(req.url, `http://${req.headers.host || 'localhost'}`);
  const pathname = url.pathname;

  res.json = (data, status = 200) => {
    res.writeHead(status, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify(data, null, 2));
  };

  req.body = async () => {
    const buffers = [];
    for await (const chunk of req) buffers.push(chunk);
    const raw = Buffer.concat(buffers).toString();
    try {
      return raw ? JSON.parse(raw) : {};
    } catch {
      return {};
    }
  };

  try {
    if (pathname === '/' || pathname === '') {
      return res.json({
        project: config.projectName,
        architecture: 'native-node-modules (Zero Dependencies)',
        status: 'running',
        endpoints: ['/health', '/api/users']
      });
    }

    if (pathname.startsWith('/health')) {
      return handleHealthRoutes(req, res, pathname);
    }

    if (pathname.startsWith('/api/users')) {
      return await handleUserRoutes(req, res, pathname);
    }

    res.json({ error: 'Not Found', path: pathname }, 404);
  } catch (err) {
    res.json({ error: 'Internal Server Error', message: err.message }, 500);
  }
});

server.listen(config.port, () => {
  console.log(`[${config.projectName}] Native Node.js modular server listening on http://localhost:${config.port}`);
  console.log(`[Health] Ready at /health`);
});

export default server;
