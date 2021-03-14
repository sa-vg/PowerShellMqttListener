using System;
using System.Collections.Concurrent;
using System.Threading;
using MQTTnet;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MQTTnet.Extensions.ManagedClient;

namespace PowerShellMqtt.Listener
{
    internal class Listener : IDisposable
    {
        private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(15);
        private readonly AutoResetEvent _connectConfirmation = new(false);
        private readonly AutoResetEvent _disconnectConfirmation = new(false);
        public BlockingCollection<MqttApplicationMessage> Inbox { get; } = new();
        private readonly IManagedMqttClient _client;

        private Listener()
        {
            _client = new MqttFactory().CreateManagedMqttClient();
            _client.ConnectedHandler = new MqttClientConnectedHandlerDelegate(OnSubscriberConnected);
            _client.DisconnectedHandler = new MqttClientDisconnectedHandlerDelegate(OnSubscriberDisconnected);
            _client.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(OnSubscriberMessageReceived);
        }

        public static Listener Start(IMqttClientOptions options)
        {
            var client = new Listener();
            client.Connect(options);
            return client;
        }

        public void Subscribe(string topic)
        {
            var topicFilter = new MqttTopicFilter
            {
                Topic = topic
            };

            _client.SubscribeAsync(topicFilter).GetAwaiter().GetResult();
        }

        private void Connect(IMqttClientOptions options)
        {
            _client.StartAsync(new ManagedMqttClientOptions {ClientOptions = options}).GetAwaiter().GetResult();
            if (!_connectConfirmation.WaitOne(ConnectionTimeout))
            {
                throw new Exception("Failed to connect. Timeout reached.");
            }
        }

        private void WaitDisconnect()
        {
            _client.StopAsync().GetAwaiter().GetResult();

            Console.WriteLine($"Waiting disconnect confirmation. {DateTime.Now} Timeout: {ConnectionTimeout}");
            if (_disconnectConfirmation.WaitOne(ConnectionTimeout))
            {
                Console.WriteLine($"Disconnect confirmed: {DateTime.Now}");
            }
            else throw new Exception($"Failed to receive disconnect confirmation in {ConnectionTimeout}");
        }

        private void OnSubscriberConnected(MqttClientConnectedEventArgs connection)
        {
            Console.WriteLine($"Client connection established.");
            _disconnectConfirmation.Reset();
            _connectConfirmation.Set();
        }

        private void OnSubscriberDisconnected(MqttClientDisconnectedEventArgs disconnection)
        {
            Console.WriteLine($"Subscriber disconnected: Reason: {disconnection.Reason} {disconnection.Exception}");
            _disconnectConfirmation.Set();
        }

        private void OnSubscriberMessageReceived(MqttApplicationMessageReceivedEventArgs x)
        {
            Inbox.Add(x.ApplicationMessage);
        }

        public void Dispose()
        {
            try
            {
                Console.WriteLine($"Stopping client...");

                if (_client.IsConnected)
                {
                    WaitDisconnect();
                }
                else
                {
                    Console.WriteLine($"Client was already disconnected.");
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Failed to stop client. {exception}");
                throw;
            }
            finally
            {
                _client.Dispose();
            }
        }
    }
}