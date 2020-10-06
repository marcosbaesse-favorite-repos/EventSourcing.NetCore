using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Carts.Carts.Events;
using Carts.Carts.ValueObjects;
using Carts.Pricing;
using Core.Aggregates;

namespace Carts.Carts
{
    public class Cart: Aggregate
    {
        public Guid ClientId { get; private set; }

        public CartStatus Status { get; private set; }

        public IList<PricedProductItem> ProductItems { get; private set; }

        public static Cart Initialize(
            Guid cartId,
            Guid clientId)
        {
            return new Cart(cartId, clientId);
        }

        private Cart(
            Guid id,
            Guid clientId)
        {
            Guard.Against.Default(id, nameof(id));
            Guard.Against.Default(clientId, nameof(clientId));

            var @event = CartInitialized.Create(
                id,
                clientId,
                CartStatus.Pending
            );

            Enqueue(@event);
            Apply(@event);
        }

        private void Apply(CartInitialized @event)
        {
            Version++;

            Id = @event.CartId;
            ClientId = @event.ClientId;
            ProductItems = new List<PricedProductItem>();
            Status = @event.CartStatus;
        }

        public void AddProduct(
            IProductPriceCalculator productPriceCalculator,
            ProductItem productItem)
        {
            if(Status != CartStatus.Pending)
                throw new InvalidOperationException($"Adding product for the cart in '{Status}' status is not allowed.");

            var pricedProductItem = productPriceCalculator.Calculate(productItem).Single();

            var @event = ProductAdded.Create(Id, pricedProductItem);

            Enqueue(@event);
            Apply(@event);
        }

        private void Apply(ProductAdded @event)
        {
            Version++;

            var newProductItem = @event.ProductItem;

            var existingProductItem = ProductItems.FirstOrDefault(pi => pi.MatchesProductAndPrice(newProductItem));

            if (existingProductItem is null)
            {
                ProductItems.Add(newProductItem);
                return;
            }

            var indexOfExistingItem = ProductItems.IndexOf(existingProductItem);
            ProductItems[indexOfExistingItem] = existingProductItem.SumQuantity(newProductItem);
        }

        public void RemoveProduct(
            PricedProductItem pricedProductItem)
        {
            if(Status != CartStatus.Pending)
                throw new InvalidOperationException($"Removing product from the cart in '{Status}' status is not allowed.");

            var @event = ProductRemoved.Create(Id, pricedProductItem);

            Enqueue(@event);
            Apply(@event);
        }

        private void Apply(ProductRemoved @event)
        {
            Version++;

            var existingProductItem = ProductItems.FirstOrDefault(pi => pi.MatchesProductAndPrice(@event.ProductItem));

            ProductItems.Remove(existingProductItem);
        }

        public void Confirm()
        {
            if(Status != CartStatus.Pending)
                throw new InvalidOperationException($"Confirming cart in '{Status}' status is not allowed.");

            var @event = CartConfirmed.Create(Id, DateTime.UtcNow);

            Enqueue(@event);
            Apply(@event);
        }

        private void Apply(CartConfirmed @event)
        {
            Version++;

            Status = CartStatus.Confirmed;
        }
    }
}