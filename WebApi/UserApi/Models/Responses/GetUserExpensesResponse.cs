namespace UserApi.Models.Responses
{
    public class UserExpenseDto
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public DateTime Date { get; set; }
    }
    public class GetUserExpensesResponse
    {
        public List<UserExpenseDto> Expenses { get; set; }
    }
}
