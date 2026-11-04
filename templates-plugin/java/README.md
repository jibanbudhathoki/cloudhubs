# {{PROJECT_NAME}}

Java application built with Maven following standard project hierarchy.

## Getting Started

### Prerequisites
- Java 21+
- Apache Maven 3.9+

### Building & Running
```bash
# Compile and package
mvn clean package

# Run packaged application
java -jar target/{{PROJECT_SLUG}}-1.0.0-SNAPSHOT.jar
```

The application runs on `http://localhost:8080`.

## Structure
- `src/main/java/com/cloudhubs/app/Application.java` - Application main class and HTTP handlers
- `src/main/resources/application.properties` - Application configuration
- `src/test/java/` - Unit tests
- `pom.xml` - Maven project descriptor

