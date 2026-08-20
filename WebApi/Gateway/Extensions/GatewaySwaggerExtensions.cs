namespace Gateway.Extensions
{
    /// <summary>
    /// Barcha backend servislarning Swagger hujjatlarini bitta UI'da yig'adi.
    ///
    /// Swagger JSON gateway'ning o'zi orqali olinadi (<c>/{servis}/swagger/v1/swagger.json</c>),
    /// shuning uchun downstream servislar tashqariga chiqarilmagan bo'lsa ham hujjat ko'rinadi.
    /// Downstream tomonda <c>UseSwaggerIfEnabled</c> <c>X-Forwarded-Prefix</c> ni o'qib
    /// <c>servers</c> URL'ini to'g'rilaydi — "Try it out" gateway orqali ishlaydi.
    ///
    /// Production'da <c>Swagger:Enabled</c> false — API yuzasi oshkor qilinmaydi.
    /// </summary>
    public static class GatewaySwaggerExtensions
    {
        private static readonly (string Route, string Title)[] Services =
        {
            ("auth",    "Auth API"),
            ("user",    "User API"),
            ("admin",   "Admin API"),
            ("session", "Session API"),
            ("billing", "Billing API"),
            ("payment", "Payment API"),
            ("device",  "Device API")
        };

        public static WebApplication MapGatewaySwaggerUi(this WebApplication app)
        {
            if (!app.Configuration.GetValue("Swagger:Enabled", false))
                return app;

            app.UseSwaggerUI(options =>
            {
                foreach (var (route, title) in Services)
                    options.SwaggerEndpoint($"/{route}/swagger/v1/swagger.json", title);

                options.RoutePrefix = "swagger";
                options.DocumentTitle = "BotEnergy API Gateway";
            });

            return app;
        }
    }
}
