package handlers

import (
	"encoding/json"
	"net/http"
)

type InfoResponse struct {
	Name        string   `json:"name"`
	Language    string   `json:"language"`
	Endpoints   []string `json:"endpoints"`
}

func RegisterRoutes(mux *http.ServeMux) {
	mux.HandleFunc("GET /health", HealthHandler)
	mux.HandleFunc("GET /api/info", InfoHandler)
	mux.HandleFunc("GET /", HomeHandler)
}

func HomeHandler(w http.ResponseWriter, r *http.Request) {
	if r.URL.Path != "/" {
		http.NotFound(w, r)
		return
	}
	w.Header().Set("Content-Type", "application/json")
	_ = json.NewEncoder(w).Encode(map[string]string{
		"message": "Welcome to {{PROJECT_NAME}} Go Service",
		"docs":    "/health",
	})
}

func InfoHandler(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")
	_ = json.NewEncoder(w).Encode(InfoResponse{
		Name:      "{{PROJECT_NAME}}",
		Language:  "Go 1.22+",
		Endpoints: []string{"/health", "/api/info"},
	})
}

