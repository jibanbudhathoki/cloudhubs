const express = require('express');
const homeController = require('../controllers/homeController');
const itemsController = require('../controllers/itemsController');

const router = express.Router();

router.get('/', homeController.index);
router.get('/health', homeController.status);

router.get('/api/items', itemsController.list);
router.get('/api/items/:id', itemsController.get);
router.post('/api/items', itemsController.create);
router.delete('/api/items/:id', itemsController.delete);

module.exports = router;

