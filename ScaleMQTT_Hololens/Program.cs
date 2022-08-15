using StereoKit;
using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Security.Authentication;

using MQTTnet;
using MQTTnet.Client;

namespace ScaleMQTT
{
    class ScaleMQTT
    {

        private static readonly MqttFactory mqttFactory = new MqttFactory();
        private IMqttClient mqttClient;

        private static readonly string brokerHostName = "f4c677e57df34db699b7f61887f40fb4.s1.eu.hivemq.cloud";
        private static readonly int brokerPort = 8883;

        private const string username = "IOTscale";
        private const string password = "12345678";

        private float weight = 0f;
        private readonly string unit = "kg";

        private static Material floorMaterial;
        private static Matrix floorTransform;

        Pose pose;
        TextStyle style;


        public ScaleMQTT()
        {
            pose = new Pose(0, 0.1f, -0.4f, Quat.LookDir(0, 0, 1));
            style = Text.MakeStyle(Font.FromFile("C:/Windows/Fonts/Calibri.ttf") ?? Default.Font, 6 * U.cm, Color.HSV(0.26f, 0.99f, 0.93f));
        }


        public Task ApplicationMessageReceivedHandler(MqttApplicationMessageReceivedEventArgs e)
        {


            Byte[] data = e.ApplicationMessage.Payload;

            string message = System.Text.Encoding.Default.GetString(data);

            weight = float.Parse(message, CultureInfo.InvariantCulture.NumberFormat);

            Console.WriteLine(weight.ToString("00.0"));

            return Task.CompletedTask;


        }

        public async Task<MqttClientSubscribeResult> Subscribe()
        {

            Console.WriteLine("Subscribe");
            // Setup message handling before connecting so that queued messages
            // are also handled properly. When there is no event handler attached all
            // received messages get lost.
            mqttClient.ApplicationMessageReceivedAsync += ApplicationMessageReceivedHandler;

            var mqttSubscribeOptions = mqttFactory
                .CreateSubscribeOptionsBuilder()
                .WithTopicFilter(f => { f.WithTopic("scale/value"); })
                .Build();

            using (var timeout = new System.Threading.CancellationTokenSource(5000))
            {
                MqttClientSubscribeResult mqttClientSubscribeResult = await mqttClient.SubscribeAsync(mqttSubscribeOptions, timeout.Token);
                Console.WriteLine("MQTT client subscribed to topic. scale/value");
                return mqttClientSubscribeResult;
            }

        }

        /*
        public async Task<MqttClientPublishResult> Publish()
        {

            Console.WriteLine("Publish");

            var applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic("scale/value")
                .WithPayload("19.5")
                .Build();

            using (var timeout = new System.Threading.CancellationTokenSource(5000))
            {
                MqttClientPublishResult mqttClientPublishResult = await mqttClient.PublishAsync(applicationMessage, timeout.Token);
                Console.WriteLine("MQTT application message is published.");
                return mqttClientPublishResult;
            }
        }
        */

        public async Task<MqttClientConnectResult> Connect()
        {

            mqttClient = mqttFactory.CreateMqttClient();

            var mqttClientOptions = new MqttClientOptionsBuilder()

                .WithTcpServer(brokerHostName, brokerPort)
                .WithCredentials(username, password)
                .WithTls(
                    optionsBuilder =>
                    {
                        // The used public broker sometimes has invalid certificates. This sample accepts all
                        // certificates. This should not be used in live environments.
                        optionsBuilder.CertificateValidationHandler = _ => true;

                        // The default value is determined by the OS. Set manually to force version.
                        optionsBuilder.SslProtocol = SslProtocols.Tls12;
                    })
                .Build();

            MqttClientConnectResult mqttClientConnectResult = await mqttClient.ConnectAsync(mqttClientOptions);

            return mqttClientConnectResult;

        }

        public async void MqttTasks()
        {
            await Connect();
            await Subscribe();
            //await Publish();

        }

        // Update the UI
        public void Update()
        {
            UI.WindowBegin("DigitalTwin: IoT Scale", ref pose, new Vec2(40, 0) * U.cm);
            UI.PushTextStyle(style);
            UI.Text(weight.ToString("00.0") + " " + unit, TextAlign.XCenter | TextAlign.YCenter);
            UI.PopTextStyle();
            UI.WindowEnd();
        }

        static void Main()
        {

            // Initialize StereoKit
            SKSettings settings = new SKSettings
            {
                appName = "StereoKitMQTT",
                assetsFolder = "Assets",
            };
            if (!SK.Initialize(settings))
                Environment.Exit(1);


            ScaleMQTT scaleMQTT = new ScaleMQTT();
            scaleMQTT.MqttTasks();

            // VR and Desktop environments need a floor
            if (SK.System.displayType == Display.Opaque)
            {
                floorTransform = Matrix.TS(0, -1.5f, 0, new Vec3(30, 0.1f, 30));

                floorMaterial = new Material(Shader.FromFile("floor.hlsl"))
                {
                    Transparency = Transparency.Blend
                };
            }


            // Core application loop
            while (SK.Step(() =>
            {
                // VR and Desktop environments need a floor
                if (SK.System.displayType == Display.Opaque) Default.MeshCube.Draw(floorMaterial, floorTransform);
                scaleMQTT.Update();

            })) ;

            SK.Shutdown();
        }
    }
}
