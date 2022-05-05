using Azure.Messaging.ServiceBus;
using Mango.MessageBus;
using Mango.Services.PaymentAPI.Messages;
using Newtonsoft.Json;
using PaymentProcessor;
using System.Text;

namespace Mango.Services.PaymentAPI.Messaging
{
    public class AzureServiceBusConsumer : IAzureServiceBusConsumer
    {
        private readonly string serviceBusConnectionString, subscriptionNamePayment, paymentTopic, paymentUpdateTopic;
        private ServiceBusProcessor orderPaymentProccessor;
        private readonly IProcessPayment _processPayment;
        private readonly IConfiguration _configuration;
        private readonly IMessageBus _messageBus;

        public AzureServiceBusConsumer(IProcessPayment processPayment, IConfiguration configuration, IMessageBus messageBus)
        {
            this._processPayment = processPayment;
            _configuration = configuration;
            this._messageBus = messageBus;
            serviceBusConnectionString = _configuration.GetValue<string>("AzureServiceAPI:ServiceEndPoint");
            subscriptionNamePayment = _configuration.GetValue<string>("AzureServiceAPI:Subscription");
            paymentTopic = _configuration.GetValue<string>("AzureServiceAPI:Topics:Payment");
            paymentUpdateTopic = _configuration.GetValue<string>("AzureServiceAPI:Topics:UpdatePayment");

            var client = new ServiceBusClient(serviceBusConnectionString);
            orderPaymentProccessor = client.CreateProcessor(paymentTopic, subscriptionNamePayment);

        }

        public async Task Start()
        {
            orderPaymentProccessor.ProcessMessageAsync += ProcessPayments;
            orderPaymentProccessor.ProcessErrorAsync += ErrorHandler;

            await orderPaymentProccessor.StartProcessingAsync();
        }

        public async Task Stop()
        {
            await orderPaymentProccessor.StopProcessingAsync();
            await orderPaymentProccessor.DisposeAsync();
        }



        private Task ErrorHandler(ProcessErrorEventArgs arg)
        {
            Console.WriteLine(arg.Exception.ToString());
            return Task.CompletedTask;
        }

        private async Task ProcessPayments(ProcessMessageEventArgs args)
        {
            var message = args.Message;
            var body = Encoding.UTF8.GetString(message.Body);

            PaymentRequestMessage paymentRqMsg = JsonConvert.DeserializeObject<PaymentRequestMessage>(body);

            var result = _processPayment.PaymentProcessor();

            UpdatePaymentResultMessage updatePaymentResultMessage = new()
            {
                Status = result,
                OrderId = paymentRqMsg.OrderId
            };



            try
            {
                await _messageBus.PublishMessage(updatePaymentResultMessage, paymentUpdateTopic);
                await args.CompleteMessageAsync(args.Message);
            }
            catch (Exception e)
            {

                throw;
            }

        }
    }
}
