const path = require('path');

exports.index = (req, res) => {
  res.sendFile(path.join(__dirname, '../views/index.html'));
};

exports.status = (req, res) => {
  res.json({
    status: 'ok',
    project: '{{PROJECT_NAME}}',
    architecture: 'mvc',
    timestamp: new Date().toISOString()
  });
};

