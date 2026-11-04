import { userService } from './user.service.js';

export const getUsers = (req, res) => {
  res.json(userService.getAll());
};

export const createUser = async (req, res) => {
  const body = await req.body();
  if (!body.name || !body.email) {
    return res.json({ error: 'Name and email are required.' }, 400);
  }
  const created = userService.create(body);
  res.json(created, 201);
};
