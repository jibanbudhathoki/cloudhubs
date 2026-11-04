package com.cloudhubs.app;

import com.sun.net.httpserver.HttpServer;
import com.sun.net.httpserver.HttpHandler;
import com.sun.net.httpserver.HttpExchange;

import java.io.IOException;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;

public class Application {
    public static void main(String[] args) throws IOException {
        int port = 8080;
        String envPort = System.getenv("PORT");
        if (envPort != null && !envPort.isBlank()) {
            port = Integer.parseInt(envPort);
        }

        HttpServer server = HttpServer.create(new InetSocketAddress(port), 0);

        server.createContext("/", new HttpHandler() {
            @Override
            public void handle(HttpExchange exchange) throws IOException {
                String response = "{\"project\":\"{{PROJECT_NAME}}\",\"status\":\"running\",\"framework\":\"java\"}";
                exchange.getResponseHeaders().set("Content-Type", "application/json");
                byte[] bytes = response.getBytes(StandardCharsets.UTF_8);
                exchange.sendResponseHeaders(200, bytes.length);
                try (OutputStream os = exchange.getResponseBody()) {
                    os.write(bytes);
                }
            }
        });

        server.createContext("/health", new HttpHandler() {
            @Override
            public void handle(HttpExchange exchange) throws IOException {
                String response = "{\"status\":\"healthy\",\"project\":\"{{PROJECT_NAME}}\"}";
                exchange.getResponseHeaders().set("Content-Type", "application/json");
                byte[] bytes = response.getBytes(StandardCharsets.UTF_8);
                exchange.sendResponseHeaders(200, bytes.length);
                try (OutputStream os = exchange.getResponseBody()) {
                    os.write(bytes);
                }
            }
        });

        server.setExecutor(null);
        System.out.println("Java Service {{PROJECT_NAME}} running on port " + port);
        server.start();
    }
}

