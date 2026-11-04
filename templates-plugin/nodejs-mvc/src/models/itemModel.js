// In-memory model store for starter template
let items = [
  { id: 1, title: 'Explore CloudHubs', completed: true },
  { id: 2, title: 'Build amazing apps', completed: false }
];

class ItemModel {
  static getAll() {
    return items;
  }

  static getById(id) {
    return items.find(item => item.id === parseInt(id, 10));
  }

  static create(data) {
    const newItem = {
      id: items.length ? Math.max(...items.map(i => i.id)) + 1 : 1,
      title: data.title || 'Untitled',
      completed: Boolean(data.completed)
    };
    items.push(newItem);
    return newItem;
  }

  static delete(id) {
    const prevLength = items.length;
    items = items.filter(item => item.id !== parseInt(id, 10));
    return items.length < prevLength;
  }
}

module.exports = ItemModel;

