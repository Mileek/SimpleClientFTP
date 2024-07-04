
# SimpleClientFTP

SimpleClientFTP is a basic FTP client written in C#. It allows connecting to an FTP server and performing various FTP operations such as directory listing, file uploading and downloading, creating and deleting directories, and more. The client is built using .NET sockets, ensuring efficient and reliable network communication. Additionally, SimpleClientFTP can be used as a batch file or a console application via the command line, making it versatile for automation and scripting tasks.

## Table of Contents

- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Usage](#usage)
  - [Command Examples](#command-examples)
- [Architecture](#architecture)
- [Contributing](#contributing)
- [License](#license)

## Features

- Connect to an FTP server with a username and password
- List directory contents
- Upload and download files
- Create and delete directories
- Delete files
- Support for passive mode

## Requirements

- .NET Framework

## Installation

1. Clone the repository:
   ```sh
   git clone https://github.com/Mileek/SimpleClientFTP.git
   ```

## Usage

### Command Syntax
Use the following syntax to log in:
```plaintext
ftp://<username>:<password>@<serverAddress>:<port>/<directory>/<file>
```

### Command Examples
- **List Directory:**
  ```plaintext
  list ftp://user:password@192.168.0.1:21/your/directory/
  ```

- **Create Directory:**
  ```plaintext
  mkd ftp://user:password@192.168.0.1:21/your/directory/newdir
  ```

- **Delete Directory:**
  ```plaintext
  rmd ftp://user:password@192.168.0.1:21/your/directory/olddir
  ```

- **Download File:**
  ```plaintext
  retr ftp://user:password@192.168.0.1:21/your/directory/file.txt C:\local\path\file.txt
  ```

- **Upload File:**
  ```plaintext
  stor C:\local\path\file.txt ftp://user:password@192.168.0.1:21/your/directory/file.txt
  ```

## Architecture

The main class `ClientFTP` handles the connection and communication with the FTP server. It includes methods to send commands and receive responses, manage data transfer, and handle different FTP operations.

## Contributing

Contributions are welcome! Please submit a pull request or open an issue to discuss your changes.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
