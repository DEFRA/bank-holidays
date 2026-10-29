using System.Net;

namespace DEFRA.BankHolidays.Tests
{
    // Serves canned responses so no test depends on the network.
    internal sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> respond;

        public StubHandler(Func<HttpResponseMessage> respond)
        {
            this.respond = respond;
        }

        public StubHandler(string content)
            : this(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) })
        {
        }

        public StubHandler(Exception exception)
            : this(() => throw exception)
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(respond());
        }
    }
}
