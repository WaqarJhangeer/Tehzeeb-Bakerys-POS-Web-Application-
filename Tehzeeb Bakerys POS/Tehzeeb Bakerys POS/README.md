# Tehzeeb Bakers — POS System

A simple point-of-sale application that runs as a web page in the browser.

The store needs a place to keep track of what it sells. Each product has five things recorded against it: its name, its price, its SKU (the unique code that identifies it, much like a barcode number), the category it falls under, and how many units are left on the shelf. The cashier should be able to add new products and look them up, and it makes sense to allow editing and deleting as well.

U can maintain a product catalog, open a sale, add and remove items by SKU, undo the most
recent add.

---

## Build & run

```
cd ".\Tehzeeb Bakerys POS\PosSystem"   
dotnet restore        # restore dependencies
dotnet build          # compile
dotnet run            # serves the till at http://localhost:5080

IMPORTANT NOTE make sure that the fist localhost:5080 is closed before u start again 
```

Then open <http://localhost:5080> in a browser. One screen, no menu numbers:

| On screen                | What it maps to                                                |
| ------------------------ | -------------------------------------------------------------- |
| Product grid + search    | FR-1: the catalog, filtered by name/SKU and category           |
| **+ New product**        | FR-1: register a new SKU, price, category and opening stock    |
| **Start new sale**       | FR-2: open an empty cart for a customer                        |
| **Add** on a product card| FR-3: add by SKU + quantity (a repeat SKU bumps the quantity)  |
| **×** on a cart line     | FR-4: drop a whole line item                                   |
| **↺ Undo last add**      | FR-4: reverse the most recent add (Stack)                      |
| Totals panel             | FR-5: subtotal, sales tax, grand total                         |
| **Checkout**             | FR-6: async payment, reduce stock, receipt, JSON archive       |
| **Print queue**          | Drain the FIFO print spool to `receipts/*.txt`                 |
| **Save catalog**         | Write `catalog.json` via Newtonsoft.Json                       |
| **Tax brackets**         | The per-category rate table                                    |

The browser holds no business rules. It posts an action, and the server replies with the
whole till state, which the page redraws itself from.


### The JSON API behind the page

| Method   | Route                    | Does                        |
| -------- | ------------------------ | --------------------------- |
| `GET`    | `/api/state`             | Catalog, categories, tax brackets, payment methods, open cart |
| `POST`   | `/api/products`          | FR-1 add product            |
| `POST`   | `/api/catalog/save`      | Save `catalog.json`         |
| `POST`   | `/api/sale`              | FR-2 open a sale            |
| `DELETE` | `/api/sale`              | Discard the open sale       |
| `POST`   | `/api/cart/items`        | FR-3 add to cart            |
| `DELETE` | `/api/cart/items/{sku}`  | FR-4 remove a line          |
| `POST`   | `/api/cart/undo`         | FR-4 undo the last add      |
| `POST`   | `/api/checkout`          | FR-6 pay, stock, receipt    |
| `POST`   | `/api/print/flush`       | Drain the print spool       |

Every action answers `{ ok, message, state }` — a refused action is still a `200`, because
the page always wants the fresh state back, and `ok: false` is what turns the toast red.

---

## Project structure

One project. The web layer at the top is thin plumbing; the rules live in the model,
data and service classes underneath it.

```
PosSystem/
├── Program.cs                    Minimal API: one route per till action
├── TillState.cs                  The one shared till (catalog + open cart + printer)
├── Contracts.cs                  The request/response records the browser sees
├── ProductImages.cs              Matches wwwroot/images/<SKU>.<ext> to a product
├── Common/
│   └── Categories.cs             Fixed category array + lookup
├── Models/
│   ├── IEntity.cs                Key contract for the generic repository
│   ├── Product.cs                Catalog item: SKU, name, price, category, stock
│   ├── CartItem.cs               One line on the cart
│   ├── CartAction.cs             One undo-history entry
│   └── Order.cs                  The cart/sale: lines, undo stack, totals, receipt
├── Data/
│   ├── Repository.cs             Generic Repository<T> (List + Dictionary index)
│   ├── Catalog.cs                FR-1 product catalog
│   ├── SeedData.cs               Built-in starter products
│   └── JsonStore.cs              Newtonsoft.Json load/save, async, StreamReader/Writer
├── Services/
│   ├── PaymentService.cs         Awaitable ProcessAsync()
│   └── ReceiptPrinter.cs         FIFO print queue
├── Helpers/
│   ├── MoneyHelper.cs            Currency formatting/rounding
│   ├── TaxHelper.cs              Tax-bracket arrays, ref accumulator
│   ├── InventoryHelper.cs        ref-based stock adjustment
│   └── Result.cs                 Generic Result<T>
└── wwwroot/
    ├── index.html                The till screen
    ├── app.css                   Plain CSS, no framework
    ├── app.js                    Post an action, redraw from the state that comes back
    └── images/                   Drop product photos here, named after the SKU
```

`bin/` and `obj/` are build output. They are regenerated by `dotnet build` and can be
deleted at any time — handy when the file count matters for submission.

---

## Business rules

**Sales tax** is charged per category, not as one flat rate (see `TaxHelper`):

| Category      | Rate |
| ------------- | ---- |
| Bakery        | 0%   |
| Cakes         | 5%   |
| Savouries     | 5%   |
| Confectionery | 10%  |
| Beverages     | 16%  |

**Grand total** = subtotal + the sum of per-line tax.

**Stock** is checked twice — once when the item goes on the cart, and again at checkout in case
another sale drained it in between. It is only decremented after the payment is authorised.

**Prices are frozen** on the cart line the moment the item is added, so repricing a catalog product
mid-sale cannot rewrite a transaction that is already in progress.

---

And lastly you can downlode the receipt and save on your computer.
