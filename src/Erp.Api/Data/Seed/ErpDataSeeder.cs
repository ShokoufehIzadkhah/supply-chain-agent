using Erp.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Api.Data.Seed;

/// <summary>
/// Deterministic demo dataset for a Finnish/Nordic packaging manufacturer (corrugated boxes,
/// folding cartons, liquid packaging board and paper sacks).
/// A fixed random seed produces identical data on every machine; only the dates are relative
/// to the day the seeder runs. Data is written only when the database is still empty.
/// </summary>
public static class ErpDataSeeder
{
    private const int RandomSeed = 20260301;

    /// <summary>Orders are numbered from here, matching the identity seed in the initial migration.</summary>
    private const int FirstOrderId = 1001;

    /// <summary>Order kept stable for demos: InProduction, priority customer, production starts in 8 days.</summary>
    private const int DemoOrderId = 1023;

    private static readonly (string Name, string Country, bool IsPriority)[] CustomerSeed =
    [
        ("Nordvik Elintarvikkeet Oy", "Finland", true),
        ("Bergstrand Livsmedel AB", "Sweden", false),
        ("Fjordkyst Sjømat AS", "Norway", true),
        ("København Kolonial A/S", "Denmark", false),
        ("Pohjan Verkkokauppa Oy", "Finland", false),
        ("Alpenhof Lebensmittel GmbH", "Germany", true),
        ("Van Dijk Versgroep B.V.", "Netherlands", false),
        ("Maison Duval Distribution SAS", "France", false),
        ("Balti E-Commerce OÜ", "Estonia", false),
        ("Iberia Retail Ibérica S.L.", "Spain", false)
    ];

    private static readonly (string Sku, string Name, string Category, string Unit)[] ProductSeed =
    [
        ("CB-400x300x250-BC", "Corrugated Box 400x300x250 mm BC-flute", "Corrugated Box", "pcs"),
        ("CB-600x400x400-BC", "Corrugated Box 600x400x400 mm BC-flute", "Corrugated Box", "pcs"),
        ("CB-300x200x150-B", "Corrugated Box 300x200x150 mm B-flute", "Corrugated Box", "pcs"),
        ("CB-1200x800x600-AB", "Heavy-Duty Pallet Box 1200x800x600 mm AB-flute", "Corrugated Box", "pcs"),
        ("CB-TRAY-2440-CC", "Corrugated Tray 2440x760 mm C-flute", "Corrugated Box", "pcs"),
        ("FC-180x70x60-350", "Folding Carton 180x70x60 mm 350 gsm GC1", "Folding Carton", "pcs"),
        ("FC-220x120x90-400", "Folding Carton 220x120x90 mm 400 gsm GC1", "Folding Carton", "pcs"),
        ("FC-SUSHI-260-300", "Sushi Carton 260 mm 300 gsm", "Folding Carton", "pcs"),
        ("FC-CEREAL-240-350", "Cereal Carton 240x180x60 mm 350 gsm", "Folding Carton", "pcs"),
        ("FC-MEDKIT-450", "Medical Kit Carton 450 gsm", "Folding Carton", "pcs"),
        ("LPB-210-1000", "Liquid Packaging Board 210 gsm 1000 mm", "Liquid Packaging Board", "reel"),
        ("LPB-240-1200", "Liquid Packaging Board 240 gsm 1200 mm", "Liquid Packaging Board", "reel"),
        ("LPB-270-1400", "Liquid Packaging Board 270 gsm 1400 mm", "Liquid Packaging Board", "reel"),
        ("LPB-300-1600", "Liquid Packaging Board 300 gsm 1600 mm", "Liquid Packaging Board", "reel"),
        ("LPB-330-1800", "Liquid Packaging Board 330 gsm 1800 mm", "Liquid Packaging Board", "reel"),
        ("PS-10-2P", "Paper Sack 10 kg 2-ply", "Paper Sack", "pcs"),
        ("PS-25-3P", "Paper Sack 25 kg 3-ply", "Paper Sack", "pcs"),
        ("PS-50-4P", "Paper Sack 50 kg 4-ply", "Paper Sack", "pcs"),
        ("PS-FLOUR-25-3P", "Flour Sack 25 kg 3-ply", "Paper Sack", "pcs"),
        ("PS-CEMENT-50-4P", "Cement Sack 50 kg 4-ply", "Paper Sack", "pcs")
    ];

    private static readonly (string Name, string City)[] WarehouseSeed =
    [
        ("Imatra Mill Warehouse", "Imatra"),
        ("Oulu Converting Plant", "Oulu"),
        ("Gdańsk Distribution Centre", "Gdańsk")
    ];

    private static readonly (string Name, string Code)[] Carriers =
    [
        ("Nordfrakt Logistics", "NF"),
        ("Pohjola Cargo", "PC"),
        ("Baltic Freightways", "BF"),
        ("Hansa Nordic Cargo", "HNC"),
        ("EuraFreight", "EF")
    ];

    public static async Task SeedAsync(ErpDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Orders.AnyAsync(cancellationToken))
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var random = new Random(RandomSeed);

        var customers = CustomerSeed
            .Select(c => new Customer { Name = c.Name, Country = c.Country, IsPriority = c.IsPriority })
            .ToList();

        var products = ProductSeed
            .Select(p => new Product { Sku = p.Sku, Name = p.Name, Category = p.Category, Unit = p.Unit })
            .ToList();

        var warehouses = WarehouseSeed
            .Select(w => new Warehouse { Name = w.Name, City = w.City })
            .ToList();

        var basePrices = products.Select(p => NextUnitPrice(random, p.Category)).ToArray();

        db.Customers.AddRange(customers);
        db.Products.AddRange(products);
        db.Warehouses.AddRange(warehouses);

        AddInventory(db, random, products, warehouses);
        AddOrders(db, random, today, customers, products, basePrices);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void AddInventory(ErpDbContext db, Random random, List<Product> products, List<Warehouse> warehouses)
    {
        foreach (var product in products)
        {
            foreach (var warehouse in warehouses)
            {
                var quantityOnHand = NextQuantityOnHand(random, product.Unit);

                db.Inventory.Add(new Inventory
                {
                    Product = product,
                    Warehouse = warehouse,
                    QuantityOnHand = quantityOnHand,
                    // Reserved stock can never exceed what is physically on hand.
                    QuantityReserved = quantityOnHand == 0 ? 0 : random.Next(0, quantityOnHand / 3 + 1)
                });
            }
        }
    }

    private static void AddOrders(
        ErpDbContext db,
        Random random,
        DateOnly today,
        List<Customer> customers,
        List<Product> products,
        decimal[] basePrices)
    {
        var statuses = BuildStatusPlan(random);
        var priorityCustomers = customers.Where(c => c.IsPriority).ToList();
        var shipmentNumber = 100000;

        for (var i = 0; i < statuses.Count; i++)
        {
            var id = FirstOrderId + i;
            var status = statuses[i];
            var isDemoOrder = id == DemoOrderId;

            var customer = isDemoOrder
                ? priorityCustomers[random.Next(priorityCustomers.Count)]
                : customers[random.Next(customers.Count)];

            var (orderDate, productionStartDate, requestedDeliveryDate) = BuildDates(random, today, status, isDemoOrder);

            var order = new Order
            {
                Id = id,
                Customer = customer,
                OrderDate = orderDate,
                RequestedDeliveryDate = requestedDeliveryDate,
                ProductionStartDate = productionStartDate,
                Status = status
            };

            AddLines(random, order, products, basePrices);
            AddShipment(random, order, ref shipmentNumber);

            db.Orders.Add(order);
        }
    }

    /// <summary>
    /// 50 statuses: 14 Delivered, 9 InProduction, 10 Shipped, 9 Confirmed, 6 Delayed and 2 Cancelled.
    /// The demo order (1023) is pinned to InProduction, the rest are shuffled with the seeded generator
    /// so statuses are not clustered by order id but stay reproducible.
    /// </summary>
    private static List<OrderStatus> BuildStatusPlan(Random random)
    {
        var statuses = new List<OrderStatus>();
        statuses.AddRange(Enumerable.Repeat(OrderStatus.Delivered, 14));
        statuses.AddRange(Enumerable.Repeat(OrderStatus.InProduction, 8));
        statuses.AddRange(Enumerable.Repeat(OrderStatus.Shipped, 10));
        statuses.AddRange(Enumerable.Repeat(OrderStatus.Confirmed, 9));
        statuses.AddRange(Enumerable.Repeat(OrderStatus.Delayed, 6));
        statuses.AddRange(Enumerable.Repeat(OrderStatus.Cancelled, 2));

        for (var i = statuses.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (statuses[i], statuses[j]) = (statuses[j], statuses[i]);
        }

        statuses.Insert(DemoOrderId - FirstOrderId, OrderStatus.InProduction);
        return statuses;
    }

    private static (DateOnly OrderDate, DateOnly? ProductionStartDate, DateOnly RequestedDeliveryDate) BuildDates(
        Random random,
        DateOnly today,
        OrderStatus status,
        bool isDemoOrder)
    {
        if (isDemoOrder)
        {
            // Stable demo order: ordered two weeks ago, production starts in 8 days, delivery in 4 weeks.
            return (today.AddDays(-12), today.AddDays(8), today.AddDays(28));
        }

        switch (status)
        {
            case OrderStatus.Delivered:
            {
                // Delivered a while back, so the delivery date always sits in the past.
                var requested = today.AddDays(-random.Next(3, 22));
                var orderDate = requested.AddDays(-random.Next(14, 36));
                return (orderDate, orderDate.AddDays(random.Next(2, 6)), requested);
            }

            case OrderStatus.Delayed:
            {
                // Delivery date has already been missed.
                var requested = today.AddDays(-random.Next(1, 18));
                var orderDate = requested.AddDays(-random.Next(14, 36));
                return (orderDate, orderDate.AddDays(random.Next(2, 6)), requested);
            }

            case OrderStatus.Shipped:
            {
                var orderDate = today.AddDays(-random.Next(4, 26));
                return (orderDate, orderDate.AddDays(random.Next(2, 6)), today.AddDays(random.Next(2, 20)));
            }

            case OrderStatus.InProduction:
            {
                var orderDate = today.AddDays(-random.Next(6, 31));
                return (orderDate, today.AddDays(random.Next(-12, 3)), today.AddDays(random.Next(10, 30)));
            }

            case OrderStatus.Confirmed:
            {
                // Acknowledged but not yet released to production.
                var orderDate = today.AddDays(-random.Next(0, 10));
                return (orderDate, null, today.AddDays(random.Next(14, 30)));
            }

            default:
            {
                var orderDate = today.AddDays(-random.Next(10, 45));
                return (orderDate, null, orderDate.AddDays(random.Next(14, 36)));
            }
        }
    }

    private static void AddLines(Random random, Order order, List<Product> products, decimal[] basePrices)
    {
        var lineCount = random.Next(1, 5);
        var chosen = new List<int>(lineCount);

        // A product may only appear once per order (composite primary key on OrderLines).
        while (chosen.Count < lineCount)
        {
            var index = random.Next(products.Count);
            if (!chosen.Contains(index))
            {
                chosen.Add(index);
            }
        }

        foreach (var index in chosen)
        {
            var product = products[index];
            var quantity = product.Unit == "reel"
                ? random.Next(2, 60)
                : random.Next(500, 25000);

            var unitPrice = decimal.Round(basePrices[index] * (0.92m + (decimal)random.NextDouble() * 0.16m), 2);

            order.Lines.Add(new OrderLine
            {
                Order = order,
                Product = product,
                Quantity = quantity,
                UnitPrice = unitPrice
            });
        }
    }

    private static void AddShipment(Random random, Order order, ref int shipmentNumber)
    {
        var status = order.Status switch
        {
            OrderStatus.Shipped => ShipmentStatus.InTransit,
            OrderStatus.Delivered => ShipmentStatus.Delivered,
            OrderStatus.Delayed => ShipmentStatus.Delayed,
            _ => ShipmentStatus.Pending
        };

        if (status == ShipmentStatus.Pending)
        {
            return;
        }

        var carrier = Carriers[random.Next(Carriers.Length)];

        var estimatedDelivery = status == ShipmentStatus.Delayed
            ? order.RequestedDeliveryDate.AddDays(random.Next(3, 15))
            : order.RequestedDeliveryDate;

        order.Shipments.Add(new Shipment
        {
            Order = order,
            Carrier = carrier.Name,
            Status = status,
            TrackingNumber = $"{carrier.Code}-{++shipmentNumber}",
            EstimatedDeliveryDate = estimatedDelivery,
            // Delivered orders arrive on time or a day or two late; everything else is still open.
            DeliveredDate = status == ShipmentStatus.Delivered
                ? estimatedDelivery.AddDays(random.Next(0, 3))
                : null
        });
    }

    private static decimal NextUnitPrice(Random random, string category) => category switch
    {
        "Corrugated Box" => NextMoney(random, 0.35m, 6.50m),
        "Folding Carton" => NextMoney(random, 0.08m, 0.65m),
        "Liquid Packaging Board" => NextMoney(random, 850m, 2650m),
        "Paper Sack" => NextMoney(random, 0.15m, 0.95m),
        _ => NextMoney(random, 1m, 10m)
    };

    private static decimal NextMoney(Random random, decimal min, decimal max)
        => decimal.Round(min + (decimal)random.NextDouble() * (max - min), 2);

    private static int NextQuantityOnHand(Random random, string unit)
    {
        var isReel = unit == "reel";

        return random.Next(100) switch
        {
            // Roughly 7% of stock rows are empty and 18% are running low.
            < 7 => 0,
            < 25 => isReel ? random.Next(2, 8) : random.Next(50, 500),
            _ => isReel ? random.Next(40, 260) : random.Next(2000, 45000)
        };
    }
}
