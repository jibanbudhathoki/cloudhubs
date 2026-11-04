const http = require('http');
const fs = require('fs');
const path = require('path');
const ItemModel = require('./models/itemModel');

const port = process.env.PORT || 3000;

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

  const getBody = async () => {
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
    // Routes
    if (pathname === '/' && req.method === 'GET') {
      const htmlPath = path.join(__dirname, 'views', 'index.html');
      if (fs.existsSync(htmlPath)) {
        res.writeHead(200, { 'Content-Type': 'text/html' });
        return fs.createReadStream(htmlPath).pipe(res);
      }
      return res.json({ project: '{{PROJECT_NAME}}', architecture: 'mvc' });
    }

    if (pathname === '/health' && req.method === 'GET') {
      return res.json({
        status: 'ok',
        project: '{{PROJECT_NAME}}',
        architecture: 'mvc (Zero Dependencies)',
        timestamp: new Date().toISOString()
      });
    }

    if (pathname === '/api/items' && req.method === 'GET') {
      return res.json({ items: ItemModel.getAll() });
    }

    if (pathname === '/api/items' && req.method === 'POST') {
      const body = await getBody();
      if (!body.title) {
        return res.json({ error: 'Title is required' }, 400);
      }
      const item = ItemModel.create({ title: body.title, completed: !!body.completed });
      return res.json({ item }, 201);
    }

    if (pathname.startsWith('/api/items/') && req.method === 'GET') {
      const id = pathname.split('/').pop();
      const item = ItemModel.getById(id);
      if (!item) return res.json({ error: 'Item not found' }, 404);
      return res.json({ item });
    }

    if (pathname.startsWith('/api/items/') && req.method === 'DELETE') {
      const id = pathname.split('/').pop();
      const success = ItemModel.delete(id);
      if (!success) return res.json({ error: 'Item not found' }, 404);
      res.writeHead(204);
      return res.end();
    }

    res.json({ error: 'Not Found', path: pathname }, 404);
  } catch (err) {
    res.json({ error: 'Internal Server Error', message: err.message }, 500);
  }
});

if (process.env.NODE_ENV !== 'test') {
  server.listen(port, () => {
    console.log(`[MVC Server] Zero-dependency server running at http://localhost:${port}`);
    console.log(`[Health] Ready at /health`);
  });
}

module.exports = server;
