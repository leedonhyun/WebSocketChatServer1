# WebSocket Chat Server

This project is a .NET-based WebSocket server for a real-time chat application. It manages client connections, chat rooms, and message broadcasting.

## Features

- **WebSocket Communication**: Handles real-time, two-way communication with chat clients.
- **Client Management**: Manages connected clients, including registration and username assignment.
- **Room Management**: Supports creation of and joining chat rooms. Users can send messages to specific rooms.
- **Message Broadcasting**: Broadcasts messages to all users in a specific room.
- **Command Processing**: A flexible command processing system (`ICommandProcessor`) to handle various client actions like creating rooms (`CreateRoomCommandProcessor`), joining rooms (`JoinRoomCommandProcessor`), and sending messages (`RoomMessageCommandProcessor`).
- **Scalability**: Uses Redis for distributed client management (`DistributedClientManager`) and message broadcasting (`RedisMessageBroadcaster`), allowing the server to scale across multiple instances.
- **Monitoring**: Includes a monitoring service (`MonitoringService`) to track system status, such as active connections and rooms.

## Core Components

- **`ChatMessageHandler`**: The main entry point for incoming WebSocket messages. It dispatches messages to the appropriate command processors.
- **`IClientManager`**: Interface for managing client connections. `ClientManager` provides a local in-memory implementation, while `DistributedClientManager` provides a Redis-based implementation for multi-server setups.
- **`IRoomManager`**: Interface for managing chat rooms, including members and state.
- **`IMessageBroadcaster`**: Interface for sending messages to clients. `RedisMessageBroadcaster` leverages Redis Pub/Sub to enable messaging across different server instances.
- **`ICommandProcessor`**: A generic interface for handling commands sent by clients. Each command has its own processor.

## Getting Started

1.  **Prerequisites**: .NET 9 SDK, and a Redis server (for distributed features).
2.  **Configuration**: Set up your application settings, including the Redis connection string if applicable.
3.  **Run the server**: Launch the project. The server will start listening for WebSocket connections.

## Available Commands

- `createRoom <roomName>`: Creates a new chat room.
- `joinRoom <roomId>`: Joins an existing chat room.
- `roomMessage <roomId> <message>`: Sends a message to a specific room.
- `sendFile <roomId> <filePath>`: (Functionality for file sending is present).