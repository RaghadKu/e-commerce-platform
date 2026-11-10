using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Inventory.Contracts.PublicApi;

public sealed record StockLevelDto(Guid ProductId, int OnHand, int Reserved, int Available);
