using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class ClientFTP
{
    private Socket dataSocket;
    private string directory;
    private bool isConnected;
    private string operation;
    private string param1;
    private string param2;
    private string password;
    private int port;
    private string serverIp;
    private Socket socket;
    private string username;

    public ClientFTP(string loginUrl)
    {
        try
        {
            // Creating a Uri object from the given URL, this class is usually used with FTP requests
            var uri = new Uri(loginUrl);
            // Retrieving user information and password from the URL
            this.username = uri.UserInfo.Split(':')[0];
            this.password = uri.UserInfo.Split(':')[1];
            // Retrieving the server IP address and port from the URL
            this.serverIp = uri.Host;
            this.port = uri.Port;
            // Retrieving the directory path from the URL
            this.directory = uri.AbsolutePath;
            // Creating a new TCP socket
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public bool Connect()
    {
        try
        {
            // Establishing a connection to the FTP server
            socket.Connect(new IPEndPoint(IPAddress.Parse(serverIp), port));
            isConnected = true;

            // Starting a thread to receive responses from the server
            ReceiveResponse();
            // Sending the username and password to the server
            User(username);
            Pass(password);
            // Sending the TYPE I command to the server, which sets the data transfer mode to binary
            TypeI();
            // Sending the MODE S command to the server, which sets the data transfer mode to stream
            ModeS();
            // Sending the STRU F command to the server, which sets the file structure to file (F)
            StruF();
            // If a directory is specified, sending the CWD (Change Working Directory) command to the server during login
            if (!string.IsNullOrEmpty(directory))
            {
                SendCommand($"CWD {directory}\r\n");
            }

            return true;
        }
        catch (Exception e)
        {
            // Displaying an error if the connection could not be established
            Console.WriteLine($"Error connecting: {e.Message}");
            return false;
        }
    }

    public void CreateCommand(string operation, string param1, string param2)
    {
        // Pass what you received from the client to global variables for further handling
        this.param2 = param2;
        this.param1 = param1;
        this.operation = operation;
        switch (operation.ToUpper())
        {
            case "LIST":
            case "LS":// ls
                Pasv();
                break;

            case "MKD":
            case "MKDIR":// mkdir
                MKD(param1);
                // Disconnect();
                break;

            case "RMD":
            case "RMDIR":// rm
                RMD(param1);
                // Disconnect();
                break;

            case "RETR":// retrieve, download
            case "CP":
                Pasv();
                break;

            case "STOR":// store, save in connection creating cp i.e., "Copy"
            case "MV":
                Pasv();
                break;

            case "RNFR":// rename from, change from
                break;

            case "RNTO":// rename to, change to together they create mv i.e., "Moving"
                break;

            case "DELE":
            case "RM":
                DELE(param1);
                //Disconnect();
                break;

            default:
                Console.WriteLine("Unknown FTP command.");
                break;
        }
    }

    public void Disconnect()
    {
        if (dataSocket != null && dataSocket.Connected)
        {
            dataSocket.Close();
        }

        if (socket.Connected)
        {
            Quit();
            isConnected = false;
            socket.Close();
        }
    }

    public void ReceiveDataResponse()
    {
        // Receive responses from the data link
        new Thread(() =>
        {
            try
            {
                // Check if the data socket is connected
                if (dataSocket != null && dataSocket.Connected)
                {
                    // Create a buffer for receiving data
                    var buffer = new byte[2048];
                    int received = dataSocket.Receive(buffer, 0, buffer.Length, 0);

                    // Adjust the buffer size to the actual amount of data received
                    Array.Resize(ref buffer, received);
                    string response = Encoding.ASCII.GetString(buffer);
                    Console.WriteLine(response);
                }
                else
                {
                    Console.WriteLine("Data socket is not connected.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Debug.WriteLine(e);
            }
        }).Start();
    }

    public void ReceiveResponse()
    {
        new Thread(() =>
        {
            try
            {
                while (isConnected)
                {
                    // Create a sufficiently large buffer for messages
                    var buffer = new byte[8192];
                    int received;
                    string response = "";

                    // Receive data available on the socket
                    while ((received = socket.Available) > 0)
                    {
                        var partialBuffer = new byte[received];
                        socket.Receive(partialBuffer, 0, received, 0);
                        response += Encoding.ASCII.GetString(partialBuffer);
                    }

                    if (response != string.Empty)
                    {
                        // Split the response into lines
                        string[] lines = response.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                        // Handle each line
                        foreach (var line in lines)
                        {
                            var handled = HandleResponse(line);
                            if (!handled)
                            {
                                Console.WriteLine(line);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("An exception occurred: " + e.ToString());
            }
        }).Start();
    }

    public void SendCommand(string command)
    {
        byte[] cmdBytes = Encoding.ASCII.GetBytes(command);
        socket.Send(cmdBytes, cmdBytes.Length, 0);
    }

    private void CWD(string param1)// Change to directory
    {
        if (Uri.TryCreate(param1, UriKind.Absolute, out Uri uri))
        {
            string directory = Path.GetDirectoryName(uri.AbsolutePath);
            if (!string.IsNullOrEmpty(directory) && directory != "\\")
            {
                SendCommand($"CWD {directory}\r\n");
            }
        }
    }

    private void DataTransfer(string response)
    {
        //"227 Entering passive mode (192,168,150,90,195,149)"
        string[] parts = response.Split(','); // Splits the response into parts
        int portHigh = int.Parse(parts[parts.Length - 2]); // High byte of the port
        int portLow = int.Parse(parts[parts.Length - 1].Split(')')[0]); // Low byte of the port
        int port = (portHigh << 8) + portLow; // Calculates the port

        // Create a new connection to the FTP server as a data channel
        dataSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        dataSocket.Connect(new IPEndPoint(IPAddress.Parse(serverIp), port));

        ReceiveDataResponse();
    }

    private void DELE(string param1)
    {
        if (Uri.TryCreate(param1, UriKind.Absolute, out Uri uri))
        {
            string filePath = uri.AbsolutePath;
            if (!string.IsNullOrEmpty(filePath) && filePath != "/")
            {
                SendCommand($"DELE {filePath}\r\n");
            }
        }
        else
        {
            Console.WriteLine("Invalid URL.");
        }
    }

    private void DownloadFile(string fileName)
    {
        using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
        {
            byte[] buffer = new byte[2048];
            int bytesRead;
            while ((bytesRead = dataSocket.Receive(buffer, 0, buffer.Length, SocketFlags.None)) > 0) // Empty file will not be downloaded
            {
                fileStream.Write(buffer, 0, bytesRead);
            }
        }
    }

    private bool HandleResponse(string response)
    {
        // Handling commands/responses from the server
        if (response.StartsWith("220"))
        {
            Console.WriteLine("Connected to FTP server");
            return true;
        }
        else if (response.StartsWith("221"))
        {
            Console.WriteLine("Disconnected from FTP server");
            return true;
        }
        else if (response.StartsWith("230"))
        {
            Console.WriteLine("Logged in on FTP server");
            return true;
        }
        else if (response.StartsWith("331"))
        {
            Console.WriteLine("Enter password");
            return true;
        }
        else if (response.StartsWith("150"))
        {
            Console.WriteLine("Downloading file list");
            return true;
        }
        else if (response.StartsWith("226"))
        {
            Console.WriteLine("Data transfer complete and disconnected"); // Success returned by list, store, retr
            Disconnect(); // Since it's complete, disconnect
            return true;
        }
        else if (response.StartsWith("227"))
        {
            DataTransfer(response); // Another connection for data transfer
            if (operation == "LIST" || operation == "LS")
            {
                this.List(param1);
            }
            else if (operation == "STOR" || operation == "CP")
            {
                STOR(param1, param2);
            }
            else if (operation == "RETR" || operation == "MV")
            {
                RETR(param1, param2);
            }
            return true; // Want to receive passive mode information along with IP
        }
        else if (response.StartsWith("250"))
        {
            Console.WriteLine("Directory/file deleted");
            Disconnect(); // Since it's complete, disconnect
            return true;
        }
        else if (response.StartsWith("550"))
        {
            Console.WriteLine("Error: Requested operation not available");
            return true;
        }
        return false;
    }

    private void List(string param1)// List directory
    {
        CWD(param1);
        SendCommand("LIST\r\n");
    }

    private void MKD(string param1)
    {
        if (Uri.TryCreate(param1, UriKind.Absolute, out Uri uri))
        {
            string directory = uri.AbsolutePath;
            if (!string.IsNullOrEmpty(directory) && directory != "/")
            {
                SendCommand($"MKD {directory}\r\n");
            }
        }
        else
        {
            Console.WriteLine("Invalid URL.");
        }
    }

    private void ModeS()
    {
        SendCommand("MODE S\r\n");
    }

    private void Pass(string password)
    {
        SendCommand($"PASS {password}\r\n");
    }

    private void Pasv()
    {
        SendCommand("PASV\r\n");
    }

    private void Quit()
    {
        SendCommand("QUIT\r\n");
    }

    private void RETR(string param1, string param2)
    {
        if (Uri.TryCreate(param1, UriKind.Absolute, out Uri uri))
        {
            string fileName = Path.GetFileName(uri.AbsolutePath);
            if (!string.IsNullOrEmpty(fileName))
            {
                SendCommand($"RETR {fileName}\r\n");
                DownloadFile(param2);
            }
        }
        else
        {
            Console.WriteLine("Invalid URL.");
        }
    }

    private void RMD(string param1)
    {
        if (Uri.TryCreate(param1, UriKind.Absolute, out Uri uri))
        {
            string directory = uri.AbsolutePath;
            if (!string.IsNullOrEmpty(directory) && directory != "/")
            {
                SendCommand($"RMD {directory}\r\n");
            }
        }
        else
        {
            Console.WriteLine("Invalid URL.");
        }
    }

    private void STOR(string param1, string param2)
    {
        if (File.Exists(param1))
        {
            SendCommand($"STOR {Path.GetFileName(param1)}\r\n");
            UploadFile(param1);
        }
        else
        {
            Console.WriteLine("File does not exist.");
        }
    }

    private void StruF()
    {
        SendCommand("STRU F\r\n");
    }

    private void TypeI()
    {
        SendCommand("TYPE I\r\n");
    }

    private void UploadFile(string filePath)
    {
        using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        {
            byte[] buffer = new byte[2048];
            int bytesRead;
            while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                dataSocket.Send(buffer, bytesRead, SocketFlags.None);
            }
        }
    }

    private void User(string username)
    {
        // Log in as the user provided by the client, otherwise as anonymous
        username = username != string.Empty ? username : "anonymous";
        SendCommand($"USER {username}\r\n");
    }
}

internal class Program
{
    // Syntax for logging in: ftp://<username>:<password>@<serverAddress>:<port>/<directory>/<file>
    // Note that commands can be used interchangeably, e.g., both "LIST" and "LS" can be used for listing

    // Examples:
    // ftp://kamillocal:1@192.168.0.114:21
    // mkd ftp://kamillocal:1@192.168.0.114:21/FTPSerwer/Katalog1
    // rmd ftp://kamillocal:1@192.168.0.114:21/FTPSerwer/Katalog1
    // list ftp://kamillocal:1@192.168.0.114:21/FTPSerwer/
    // retr ftp://kamillocal:1@192.168.0.114:21/FTPSerwer/test.txt D:\FTPKlient\plik.txt
    // dele ftp://kamillocal:1@192.168.0.114:21/FTPSerwer/test.txt

    // The program works both in the debugger and as a batch file - then the args variable is used
    private static void Main(string[] args)
    {
        Console.WriteLine("To disconnect, type 'q' or press ctrl+c");
        Console.WriteLine("Syntax for logging in: ftp://<username>:<password>@<serverAddress>:<port>/<directory>/<file>");
        string loginURL;

        if (Debugger.IsAttached) // When using the debugger
        {
            Console.WriteLine("Enter the login URL:");
            loginURL = Console.ReadLine();
        }
        else // When used as a batch file
        {
            loginURL = args.Length > 0 ? args[0] : throw new ArgumentException("Login URL is required.");
        }

        var validator = ValidateLogin(loginURL); // Login validation, if someone immediately enters a command at the beginning, it will not work
        if (validator != string.Empty)
        {
            Debug.WriteLine(validator);
            return;
        }

        ClientFTP client = new ClientFTP(loginURL);

        if (client.Connect())
        {
            string command = string.Empty;
            Console.WriteLine("Enter an FTP command in the format <Command> <Parameter1> <Parameter2>.");

            while (command.ToLower() != "q")
            {
                command = Console.ReadLine();

                if (command != string.Empty)
                {
                    // Split the command into operation and parameters
                    string[] commandParts = command.Split(new char[] { ' ' }, 3);
                    string operation = commandParts[0];
                    string param1 = commandParts.Length > 1 ? commandParts[1] : string.Empty;
                    string param2 = commandParts.Length > 2 ? commandParts[2] : string.Empty;

                    // Create FTP client commands
                    client.CreateCommand(operation.ToUpper(), param1, param2);
                }
            }

            client.Disconnect();
        }
    }

    private static string ValidateLogin(string loginURL)
    {
        if (!Uri.TryCreate(loginURL, UriKind.Absolute, out Uri uri))
        {
            return "Invalid login URL.";
        }

        if (string.IsNullOrEmpty(uri.UserInfo) || !uri.UserInfo.Contains(":") || string.IsNullOrEmpty(uri.Host) || uri.Port == 0)
        {
            return "Login URL must contain username, password, server address, and port.";
        }

        return string.Empty;
    }
}