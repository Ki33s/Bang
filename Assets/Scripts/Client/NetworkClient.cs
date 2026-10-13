using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using UnityEngine;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#endif
namespace DustAndPowder
{
    public class NetworkClient : MonoBehaviour
    {
        public Action<Envelope> Received; public Action<string> Status;
        public bool Connected { get; private set; }
        readonly ConcurrentQueue<string> messages = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> statuses = new ConcurrentQueue<string>();
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void BangConnect(string url, string objectName);
        [DllImport("__Internal")] static extern void BangSend(string message);
        [DllImport("__Internal")] static extern void BangClose();
#else
        ClientWebSocket socket; CancellationTokenSource cancellation; readonly SemaphoreSlim sendGate = new SemaphoreSlim(1);
#endif
        public void Connect(string url)
        {
            Close(); Connected = false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "ws" && uri.Scheme != "wss")) { statuses.Enqueue("error:Адрес должен начинаться с ws:// или wss://"); return; }
#if UNITY_WEBGL && !UNITY_EDITOR
            BangConnect(url, gameObject.name);
#else
            cancellation = new CancellationTokenSource(); socket = new ClientWebSocket(); _ = Run(uri, socket, cancellation.Token);
#endif
        }
#if !UNITY_WEBGL || UNITY_EDITOR
        async Task Run(Uri uri, ClientWebSocket ws, CancellationToken ct)
        {
            try
            {
                await ws.ConnectAsync(uri, ct); statuses.Enqueue("open"); var buffer = new byte[65536];
                while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
                {
                    int count = 0; WebSocketReceiveResult result;
                    do { result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer, count, buffer.Length - count), ct); count += result.Count; if (count == buffer.Length) throw new Exception("Слишком большой ответ."); } while (!result.EndOfMessage);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    messages.Enqueue(Encoding.UTF8.GetString(buffer, 0, count));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { if (!ct.IsCancellationRequested) statuses.Enqueue("error:" + e.Message); }
            finally { if (!ct.IsCancellationRequested) statuses.Enqueue("close"); }
        }
#endif
        public void Send(Command command)
        {
            string message = JsonUtility.ToJson(command);
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Connected) BangSend(message);
#else
            if (Connected) _ = SendAsync(message, socket, cancellation.Token);
#endif
        }
#if !UNITY_WEBGL || UNITY_EDITOR
        async Task SendAsync(string data, ClientWebSocket ws, CancellationToken ct)
        {
            try { await sendGate.WaitAsync(ct); try { await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(data)), WebSocketMessageType.Text, true, ct); } finally { sendGate.Release(); } }
            catch (Exception e) { if (!ct.IsCancellationRequested) statuses.Enqueue("error:" + e.Message); }
        }
#endif
        public void OnSocketMessage(string value) { messages.Enqueue(value); }
        public void OnSocketStatus(string value) { statuses.Enqueue(value); }
        void Update()
        {
            while (statuses.TryDequeue(out var value)) { if (value == "open") Connected = true; if (value == "close" || value.StartsWith("error:")) Connected = false; if (Status != null) Status(value); }
            while (messages.TryDequeue(out var value)) { try { var e = JsonUtility.FromJson<Envelope>(value); if (Received != null) Received(e); } catch (Exception ex) { Debug.LogWarning(ex.Message); } }
        }
        public void Close()
        {
            Connected = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            BangClose();
#else
            if (cancellation != null) cancellation.Cancel(); if (socket != null) socket.Dispose();
#endif
            while (messages.TryDequeue(out _)) { } while (statuses.TryDequeue(out _)) { }
        }
        void OnDestroy() { Close(); }
    }
}
