using Azure.Messaging.ServiceBus;
using Mango.MessageBus;
using Mango.Services.OrderAPI.Messages;
using Mango.Services.OrderAPI.Models;
using Mango.Services.OrderAPI.Repository;
using Newtonsoft.Json;
using System.Text;

namespace Mango.Services.OrderAPI.Messaging
{
    public class AzureServiceBusConsumer : IAzureServiceBusConsumer
    {
        private readonly OrderRepository _orderRepository;
        private readonly string serviceBusConnectionString, subscriptionNameCheckOut, checkoutMessageTopic, paymentTopic, paymentUpdateTopic;
        private ServiceBusProcessor checkOutProccessor;
        private ServiceBusProcessor orderUpdatePaymentStatusProcessor;

        private readonly IConfiguration _configuration;
        private readonly IMessageBus _messageBus;

        public AzureServiceBusConsumer(OrderRepository orderRepository, IConfiguration configuration, IMessageBus messageBus)
        {
            this._orderRepository = orderRepository;
            _configuration = configuration;
            this._messageBus = messageBus;
            serviceBusConnectionString = _configuration.GetValue<string>("AzureServiceAPI:ServiceEndPoint");
            subscriptionNameCheckOut = _configuration.GetValue<string>("AzureServiceAPI:Subscription");
            checkoutMessageTopic = _configuration.GetValue<string>("AzureServiceAPI:Topics:Checkout");
            paymentTopic = _configuration.GetValue<string>("AzureServiceAPI:Topics:Payment");
            paymentUpdateTopic = _configuration.GetValue<string>("AzureServiceAPI:Topics:PaymentUpdate");

            var client = new ServiceBusClient(serviceBusConnectionString);
            checkOutProccessor = client.CreateProcessor(checkoutMessageTopic, subscriptionNameCheckOut);
            orderUpdatePaymentStatusProcessor = client.CreateProcessor(paymentUpdateTopic, subscriptionNameCheckOut);

        }

        public async Task Start()
        {
            checkOutProccessor.ProcessMessageAsync += OnCheckOutMessageReceived;
            checkOutProccessor.ProcessErrorAsync += ErrorHandler;
            await checkOutProccessor.StartProcessingAsync();

            orderUpdatePaymentStatusProcessor.ProcessMessageAsync += OnOrderPaymentUpdateReceived;
            orderUpdatePaymentStatusProcessor.ProcessErrorAsync += ErrorHandler;
            await orderUpdatePaymentStatusProcessor.StartProcessingAsync();
        }

        public async Task Stop()
        {
            await checkOutProccessor.StopProcessingAsync();
            await checkOutProccessor.DisposeAsync();

            await orderUpdatePaymentStatusProcessor.StopProcessingAsync();
            await orderUpdatePaymentStatusProcessor.DisposeAsync();
        }



        private Task ErrorHandler(ProcessErrorEventArgs arg)
        {
            Console.WriteLine(arg.Exception.ToString());
            return Task.CompletedTask;
        }

        private async Task OnCheckOutMessageReceived(ProcessMessageEventArgs args)
        {
            var message = args.Message;
            var body = Encoding.UTF8.GetString(message.Body);

            CheckoutHeaderDto checkoutHeaderDto = JsonConvert.DeserializeObject<CheckoutHeaderDto>(body);

            OrderHeader orderHeader = new()
            {
                UserId = checkoutHeaderDto.UserId,
                FirstName = checkoutHeaderDto.FirstName,
                LastName = checkoutHeaderDto.LastName,
                OrderDetails = new List<OrderDetail>(),
                CardNumber = checkoutHeaderDto.CardNumber,
                CouponCode = checkoutHeaderDto.CouponCode,
                CVV = checkoutHeaderDto.CVV,
                DiscountTotal = checkoutHeaderDto.DiscountTotal,
                Email = checkoutHeaderDto.Email,
                ExpiryMonthYear = checkoutHeaderDto.ExpiryMonthYear,
                OrderTime = DateTime.Now,
                OrderTotal = checkoutHeaderDto.OrderTotal,
                PaymentStatus = false,
                Phone = checkoutHeaderDto.Phone,
                PickupDateTime = checkoutHeaderDto.PickupDateTime
            };

            foreach (var detailList in checkoutHeaderDto.CartDetails)
            {
                OrderDetail orderDetail = new()
                {
                    ProductId = detailList.ProductId,
                    ProductName = detailList.Product.Name,
                    Price = detailList.Product.Price,
                    Count = detailList.Count
                };
                orderHeader.CartTotalItems += detailList.Count;
                orderHeader.OrderDetails.Add(orderDetail);
            }

            await _orderRepository.AddOrder(orderHeader);

            PaymentRequestMessage paymentRequestMessage = new PaymentRequestMessage()
            {
                Name = orderHeader.FirstName + " " + orderHeader.LastName,
                CardNumber = orderHeader.CardNumber,
                CVV = orderHeader.CVV,
                ExpiryMonthYear = orderHeader.ExpiryMonthYear,
                OrderId = orderHeader.OrderHeaderId,
                OrderTotal = orderHeader.OrderTotal
            };

            try
            {
                await _messageBus.PublishMessage(paymentRequestMessage, paymentTopic);
                await args.CompleteMessageAsync(args.Message);
            }
            catch (Exception e)
            {

                throw;
            }

        }


        private async Task OnOrderPaymentUpdateReceived(ProcessMessageEventArgs args)
        {
            var message = args.Message;
            var body = Encoding.UTF8.GetString(message.Body);

            UpdatePaymentResultMessage paymentResultMessage = JsonConvert.DeserializeObject<UpdatePaymentResultMessage>(body);

            await _orderRepository.UpdateOrderPaymentStatus(paymentResultMessage.OrderId, paymentResultMessage.Status);
            await args.CompleteMessageAsync(args.Message);
        }
    }
}
