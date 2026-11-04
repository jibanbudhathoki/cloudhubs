# {{PROJECT_NAME}}

Node.js web application built with Express using the Model-View-Controller (MVC) architecture.

## Getting Started

### Prerequisites
- Node.js 18+
- npm

### Installation
```bash
npm install
```

### Running Locally
```bash
# Start server
npm start

# Development mode with auto-reload
npm run dev
```

The application runs on `http://localhost:3000`.

## Architecture
- `src/controllers/` - Route handlers and business dispatch
- `src/models/` - Data models and entities
- `src/routes/` - Route definitions
- `src/views/` - View templates
- `src/server.js` - Application bootstrap and middleware setup

