import { getUsers, createUser } from './user.controller.js';

export const handleUserRoutes = async (req, res, pathname) => {
  if (req.method === 'GET' && (pathname === '/api/users' || pathname === '/api/users/')) {
    return getUsers(req, res);
  }
  if (req.method === 'POST' && (pathname === '/api/users' || pathname === '/api/users/')) {
    return await createUser(req, res);
  }
  res.json({ error: 'Not Found' }, 404);
};
