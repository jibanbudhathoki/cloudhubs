package handlers

import (
	"encoding/json"
	"net/http"
	"time"
)

type HealthResponse struct {
	Status    string    `json:"status"`
	Project   string    `json:"project"`
	Timestamp time.Time `json:"timestamp"`
}

func HealthHandler(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(http.StatusOK)
	_ = json.NewEncoder(w).Encode(HealthResponse{
		Status:    "ok",
		Project:   "{{PROJECT_NAME}}",
		Timestamp: time.Now(),
	})
}

