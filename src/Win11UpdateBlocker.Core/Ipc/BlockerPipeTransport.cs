using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

namespace Win11UpdateBlocker.Core.Ipc;

public static class BlockerPipeTransport
{
    public const string PipeName = "Win11UpdateBlocker";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static PipeSecurity CreatePipeSecurity()
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        return security;
    }

    public static BlockerPipeResponse Send(BlockerPipeRequest request, int timeoutMs = 3000)
    {
        using var client = new NamedPipeClientStream(
            ".",
            PipeName,
            PipeDirection.InOut,
            PipeOptions.None,
            TokenImpersonationLevel.Impersonation);

        var connectTask = client.ConnectAsync(timeoutMs);
        if (!connectTask.Wait(timeoutMs + 250))
        {
            throw new TimeoutException("Verbindung zum Hintergrund-Dienst hat zu lange gedauert.");
        }

        WriteMessage(client, request);
        return ReadMessage<BlockerPipeResponse>(client)
               ?? new BlockerPipeResponse { Success = false, Error = "Empty response from service." };
    }

    public static async Task RunServerLoopAsync(
        Func<BlockerPipeRequest, BlockerPipeResponse> handler,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var server = NamedPipeServerStreamAcl.Create(
                PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: 4096,
                outBufferSize: 4096,
                CreatePipeSecurity());

            try
            {
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                _ = Task.Run(() => HandleClient(server, handler), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                server.Dispose();
                break;
            }
            catch (IOException)
            {
                server.Dispose();
            }
        }
    }

    private static void HandleClient(NamedPipeServerStream server, Func<BlockerPipeRequest, BlockerPipeResponse> handler)
    {
        using (server)
        {
            try
            {
                var request = ReadMessage<BlockerPipeRequest>(server);
                var response = request is null
                    ? new BlockerPipeResponse { Success = false, Error = "Invalid request." }
                    : handler(request);
                WriteMessage(server, response);
            }
            catch (IOException)
            {
                // Client disconnected.
            }
            catch (Exception ex)
            {
                try
                {
                    WriteMessage(server, new BlockerPipeResponse { Success = false, Error = ex.Message });
                }
                catch (IOException)
                {
                    // Ignore response failures for broken clients.
                }
            }
        }
    }

    private static void WriteMessage<T>(PipeStream stream, T message)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        var lengthBytes = BitConverter.GetBytes(payload.Length);
        stream.Write(lengthBytes);
        stream.Write(payload);
        stream.Flush();
    }

    private static T? ReadMessage<T>(PipeStream stream)
    {
        var lengthBytes = ReadExact(stream, sizeof(int));
        if (lengthBytes is null)
        {
            return default;
        }

        var length = BitConverter.ToInt32(lengthBytes);
        if (length <= 0 || length > 1024 * 1024)
        {
            throw new InvalidDataException("Invalid pipe message length.");
        }

        var payload = ReadExact(stream, length);
        if (payload is null)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(payload, JsonOptions);
    }

    private static byte[]? ReadExact(PipeStream stream, int length)
    {
        var buffer = new byte[length];
        var offset = 0;

        while (offset < length)
        {
            var read = stream.Read(buffer, offset, length - offset);
            if (read == 0)
            {
                return offset == 0 ? null : throw new EndOfStreamException();
            }

            offset += read;
        }

        return buffer;
    }
}
