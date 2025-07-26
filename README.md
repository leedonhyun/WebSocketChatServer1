# .NET WebSocket Chat System

This project is a high-performance, scalable chat system built with .NET 9. It features a modular architecture with a dedicated real-time chat server and a separate RESTful API for user management.

## Architecture Overview

The system is composed of two main services:

1.  **`WebSocketChatServer`**: The core real-time service responsible for handling WebSocket connections, managing chat rooms, and broadcasting messages. It uses stream multiplexing for efficient communication.
2.  **`UserApi`**: A secure, token-based RESTful API for managing users, profiles, and authentication.

## Key Features

*   **Real-time Chat**: Low-latency, bidirectional communication using WebSockets.
*   **User Management API**: Full CRUD functionality for users, including profile updates and password management.
*   **Secure**: Token-based authentication (JWT) and role-based access control (RBAC) for protected endpoints.
*   **Stream Multiplexing**: Utilizes `Nerdbank.Streams` to handle multiple data streams (e.g., chat, file transfers) over a single connection.
*   **Scalable & Resilient**: Designed for horizontal scaling using Redis for message brokering or caching.
*   **Data Persistence**: Leverages MongoDB for storing user data and chat history.
*   **Containerized**: Fully configured for Docker, allowing for easy and consistent deployment.
*   **Observability**: Integrated with OpenTelemetry for distributed tracing, metrics, and logging.

## Technology Stack

*   **Backend**: .NET 9, ASP.NET Core
*   **Real-time Communication**: WebSockets, Nerdbank.Streams
*   **API**: RESTful Web API
*   **Database**: MongoDB (`MongoDB.EntityFrameworkCore`)
*   **Cache / Message Broker**: Redis (`StackExchange.Redis`)
*   **Security**: JWT Authentication, ASP.NET Core Identity
*   **Observability**: OpenTelemetry
*   **Deployment**: Docker

## Getting Started

### Prerequisites

*   .NET 9 SDK
*   Docker Desktop (Recommended)
*   A running MongoDB instance
*   A running Redis instance

### Configuration

1.  **Clone the repository**:
    ```sh
    git clone <your-repository-url>
    cd <project-root>
    ```

2.  **Set up Connection Strings**:
    In both the `WebSocketChatServer1` and `WebSocketChatServer.UserApi` projects, configure your `appsettings.json` with the connection details for MongoDB and Redis.

3.  **Configure JWT**:
    In `appsettings.json` for `UserApi`, configure the JWT settings (Issuer, Audience, Key).

### Running the Application

You can run the services individually using the .NET CLI or run the entire system using Docker.

**Using .NET CLI**: