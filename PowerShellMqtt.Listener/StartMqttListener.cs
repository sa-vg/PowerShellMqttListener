using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Net;
using MQTTnet;
using MQTTnet.Client.Options;
using MQTTnet.Formatter;

namespace PowerShellMqtt.Listener
{
    [Cmdlet("Start", "MqttListener", DefaultParameterSetName = "TcpServer")]
    public class StartMqttListener : Cmdlet
    {
        private static readonly TimeSpan StopSignalWaitingTimeout = TimeSpan.FromSeconds(0.2);
        
        [ValidateNotNullOrEmpty]
        [Parameter(HelpMessage = "Client_Id")]
        public string ClientId { get; set; }

        [ValidateNotNullOrEmpty]
        [Parameter(HelpMessage = "Server", Mandatory = true, ParameterSetName = "TcpServer")]
        public string Server { get; set; }

        [ValidateNotNullOrEmpty]
        [Parameter(HelpMessage = "Port")]
        public int Port { get; set; }

        [ValidateNotNullOrEmpty]
        [Parameter(HelpMessage = "Topic")]
        public string[] Topic { get; set; }

        [ValidateNotNullOrEmpty]
        [Parameter(HelpMessage = "Username")]
        public string Username { get; set; }

        [ValidateNotNullOrEmpty]
        [Parameter(HelpMessage = "Password")]
        public string Password { get; set; }
        
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, ParameterSetName = "WebSocketServer")]
        public string Uri { get; set; }
        
        [ValidateNotNullOrEmpty]
        [Parameter(Mandatory = true, ParameterSetName = "WebSocketServer")]
        public IDictionary<string, string> RequestHeaders { get; set; }
        
        [Parameter] public SwitchParameter CleanSession { get; set; }

        [Parameter] public SwitchParameter OnlyPayload { get; set; }
        
        protected override void ProcessRecord()
        {
            var options = BuildClientOptions();
            using var listener = StartListener(options);
            
            try
            {
                foreach (var topic in Topic)
                {
                    Subscribe(listener, topic);
                }
                
                ProcessMessages(listener.Inbox);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Processing stopped: {exception}");
                throw;
            }
        }

        private void ProcessMessages(BlockingCollection<MqttApplicationMessage> messages)
        {
            while (true)
            {
                CheckStopping();
                
                if (messages.TryTake(out var message, StopSignalWaitingTimeout))
                {
                    CheckStopping();
                    ProcessMessage(message);
                }
            }
            
            void ProcessMessage(MqttApplicationMessage message)
            {
                WriteVerbose($"Message received: {DateTime.Now} Topic: {message.Topic} Payload: {message.ConvertPayloadToString()}");

                if (OnlyPayload)
                {
                    WriteObject(message.ConvertPayloadToString());
                }
                else
                {
                    WriteObject(message);
                }
            }
            
            void CheckStopping()
            {
                if (Stopping)
                {
                    WriteVerbose($"Pipeline stop detected.");
                    throw new PipelineStoppedException();
                }
            }
        }

        private MqttClientOptionsBuilder BuildConnectionOptions()
        {
            var mqttClientOptionsBuilder = new MqttClientOptionsBuilder();
            if (RequestHeaders.Any())
            {
                return  mqttClientOptionsBuilder.WithWebSocketServer(BuildWebSocketServerOptions);
            }
            return  mqttClientOptionsBuilder.WithTcpServer(BuildTcpServerOptions);
        }

        private void BuildWebSocketServerOptions(MqttClientWebSocketOptions x)
        {
            x.Uri = Uri;
            x.CookieContainer = new CookieContainer();
            x.RequestHeaders = RequestHeaders;
        }

        private void BuildTcpServerOptions(MqttClientTcpOptions x)
        {
            x.Server = Server;
            x.Port = Port;
            x.TlsOptions = new MqttClientTlsOptions {UseTls = false, IgnoreCertificateChainErrors = true, IgnoreCertificateRevocationErrors = true, AllowUntrustedCertificates = true};
        }

        private IMqttClientOptions BuildClientOptions()
        {
            var options = BuildConnectionOptions()
                .WithClientId(ClientId)
                .WithCleanSession(CleanSession)
                .WithProtocolVersion(MqttProtocolVersion.V311)
                .WithCredentials(Username, Password)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(5))
                .Build();

            return options;
        }

        private void Subscribe(Listener listener, string topic)
        {
            try
            {
                WriteVerbose($"Subscribing topic: {topic}");
                listener.Subscribe(topic);
                Console.WriteLine($"Started listening on topic: {topic}");
            }
            catch (Exception exception)
            {
                WriteError(new ErrorRecord(exception, "FailedToSubscribe", ErrorCategory.ConnectionError, topic));
                WriteVerbose(exception.ToString());
                throw;
            }
        }

        private Listener StartListener(IMqttClientOptions options)
        {
            try
            {
                WriteVerbose($"Starting client with Id: {ClientId}");
                var startedClient = Listener.Start(options);
                WriteVerbose($"Started client with Id: {ClientId}");
                return startedClient;
            }
            catch (Exception exception)
            {
                WriteError(new ErrorRecord(exception, "FailedToStartClient", ErrorCategory.ConnectionError, ClientId));
                WriteVerbose(exception.ToString());
                throw;
            }
        }
    }
}