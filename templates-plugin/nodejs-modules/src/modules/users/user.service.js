let users = [
  { id: 1, name: 'Alice Developer', role: 'admin' },
  { id: 2, name: 'Bob Engineer', role: 'developer' }
];

export const userService = {
  findAll: () => users,
  findById: (id) => users.find(u => u.id === parseInt(id, 10)),
  create: (userData) => {
    const newUser = {
      id: users.length ? Math.max(...users.map(u => u.id)) + 1 : 1,
      name: userData.name,
      role: userData.role || 'developer'
    };
    users.push(newUser);
    return newUser;
  }
};

