const ItemModel = require('../models/itemModel');

exports.list = (req, res) => {
  res.json({ items: ItemModel.getAll() });
};

exports.get = (req, res) => {
  const item = ItemModel.getById(req.params.id);
  if (!item) {
    return res.status(404).json({ error: 'Item not found' });
  }
  res.json({ item });
};

exports.create = (req, res) => {
  const { title, completed } = req.body;
  if (!title) {
    return res.status(400).json({ error: 'Title is required' });
  }
  const item = ItemModel.create({ title, completed });
  res.status(201).json({ item });
};

exports.delete = (req, res) => {
  const success = ItemModel.delete(req.params.id);
  if (!success) {
    return res.status(404).json({ error: 'Item not found' });
  }
  res.status(204).send();
};

