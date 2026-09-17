using TicketAnaliz.Core.Search;

namespace TicketAnaliz.SeedGenerator.Services;

public class QuickSearchService
{
    private readonly ITicketSearchService _ticketSearchService;

    public QuickSearchService(ITicketSearchService ticketSearchService)
    {
        _ticketSearchService = ticketSearchService;
    }

    public async Task SearchAsync(string queryText, int topK = 5)
    {
        Console.WriteLine($"Sorgu: \"{queryText}\"");
        Console.WriteLine("Aranıyor...");
        Console.WriteLine();

        var results = await _ticketSearchService.SearchAsync(queryText, topK);

        foreach (var result in results)
        {
            var ticket = result.Ticket;
            Console.WriteLine($"[Skor: {result.Score:F4}] ({ticket.Status}) {ticket.Title}");
            Console.WriteLine($"  Kategori: {ticket.Category} | Departman: {ticket.Department}");
            if (ticket.Resolution is not null)
            {
                Console.WriteLine($"  Cozum: {ticket.Resolution}");
            }
            Console.WriteLine();
        }
    }
}
