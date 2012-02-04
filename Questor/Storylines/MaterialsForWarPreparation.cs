﻿namespace Questor.Storylines
{
    using System;
    using System.Linq;
    using DirectEve;
    using global::Questor.Modules;

    public class MaterialsForWarPreparation : IStoryline
    {
        private DateTime _nextAction;

        /// <summary>
        ///   Arm does nothing but get into a (assembled) shuttle
        /// </summary>
        /// <returns></returns>
        public StorylineState Arm(Storyline storyline)
        {
            if (_nextAction > DateTime.Now)
                return StorylineState.Arm; 
            
            // Are we in a shuttle?  Yes, goto the agent
            var directEve = Cache.Instance.DirectEve;
            if (directEve.ActiveShip.GroupId == 31)
                return StorylineState.GotoAgent;

            // Open the ship hangar
            var ships = directEve.GetShipHangar();
            if (ships.Window == null)
            {
                _nextAction = DateTime.Now.AddSeconds(10);

                Logging.Log("MaterialsForWarPreparation: Opening ship hangar");

                // No, command it to open
                Cache.Instance.DirectEve.ExecuteCommand(DirectCmd.OpenShipHangar);
                return StorylineState.Arm;
            }

            // If the ship hangar is not ready then wait for it
            if (!ships.IsReady)
                return StorylineState.Arm;
            
            //  Look for a shuttle
            var item = ships.Items.FirstOrDefault(i => i.Quantity == -1 && i.GroupId == 31);
            if (item != null)
            {
                Logging.Log("MaterialsForWarPreparation: Switching to shuttle");

                _nextAction = DateTime.Now.AddSeconds(10);

                item.ActivateShip();
                return StorylineState.Arm;
            }
            else
            {
                Logging.Log("MaterialsForWarPreparation: No shuttle found, going in active ship");
                return StorylineState.GotoAgent;
            }
        }

        /// <summary>
        ///   Check if we have kernite in station
        /// </summary>
        /// <returns></returns>
        public StorylineState PreAcceptMission(Storyline storyline)
        {
            var directEve = Cache.Instance.DirectEve;
            if (_nextAction > DateTime.Now)
                return StorylineState.PreAcceptMission;

            // Open the item hangar
            var hangar = directEve.GetItemHangar();
            if (hangar.Window == null)
            {
                _nextAction = DateTime.Now.AddSeconds(10);

                Logging.Log("MaterialsForWarPreparation: Opening hangar floor");

                directEve.ExecuteCommand(DirectCmd.OpenHangarFloor);
                return StorylineState.PreAcceptMission;
            }

            // Wait for the item hangar to get ready
            if (!hangar.IsReady)
                return StorylineState.PreAcceptMission;

            // Is there a market window?
            var marketWindow = directEve.Windows.OfType<DirectMarketWindow>().FirstOrDefault();

            // Do we have 8000 kernite?
            // TODO: Add lower level missions?
            if (hangar.Items.Where(i => i.TypeId == 20).Sum(i => i.Quantity) >= 8000)
            {
                Logging.Log("MaterialsForWarPreparation: We have [8000] kernite, accepting mission");

                // Close the market window if there is one
                if (marketWindow != null)
                    marketWindow.Close();

                return StorylineState.AcceptMission;
            }

            // We do not have enough kernite, open the market window
            if (marketWindow == null)
            {
                _nextAction = DateTime.Now.AddSeconds(10);

                Logging.Log("MaterialsForWarPreparation: Opening market window");

                directEve.ExecuteCommand(DirectCmd.OpenMarket);
                return StorylineState.PreAcceptMission;
            }

            // Wait for the window to become ready (this includes loading the kernite info)
            if (!marketWindow.IsReady)
                return StorylineState.PreAcceptMission;

            // Are we currently viewing kernite orders?
            if (marketWindow.DetailTypeId != 20)
            {
                // No, load the kernite orders
                marketWindow.LoadTypeId(20);

                Logging.Log("MaterialsForWarPreparation: Loading kernite into market window");

                _nextAction = DateTime.Now.AddSeconds(5);
                return StorylineState.PreAcceptMission;
            }

            // Get the median sell price
            var type = Cache.Instance.InvTypesById[20];
            var maxPrice = type.MedianSell*2;

            // Do we have orders that sell enough kernite for the mission?
            var orders = marketWindow.SellOrders.Where(o => o.StationId == directEve.Session.StationId && o.Price < maxPrice);
            if (!orders.Any() || orders.Sum(o => o.VolumeRemaining) < 8000)
            {
                Logging.Log("MaterialsForWarPreparation: Not enough (reasonably priced) kernite available! Blacklisting agent for this Questor session!");

                // Close the market window
                marketWindow.Close();

                // No, black list the agent in this Questor session (note we will never decline storylines!)
                return StorylineState.BlacklistAgent;
            }

            // How much kernite do we still need?
            var neededQuantity = 8000 - hangar.Items.Where(i => i.TypeId == 20).Sum(i => i.Quantity);
            if (neededQuantity > 0)
            {
                // Get the first order
                var order = orders.OrderBy(o => o.Price).FirstOrDefault();
                if (order != null)
                {
                    // Calculate how much kernite we still need
                    var remaining = Math.Min(neededQuantity, order.VolumeRemaining);
                    order.Buy(remaining, DirectOrderRange.Station);

                    Logging.Log("MaterialsForWarPreparation: Buying [" + remaining + "] kernite");

                    // Wait for the order to go through
                    _nextAction = DateTime.Now.AddSeconds(10);
                }
            }
            return StorylineState.PreAcceptMission;
        }

        /// <summary>
        ///   We have no combat/delivery part in this mission, just accept it
        /// </summary>
        /// <returns></returns>
        public StorylineState PostAcceptMission(Storyline storyline)
        {
            // Close the market window (if its open)
            var directEve = Cache.Instance.DirectEve;

            return StorylineState.CompleteMission;
        }

        /// <summary>
        ///   We have no execute mission code
        /// </summary>
        /// <returns></returns>
        public StorylineState ExecuteMission(Storyline storyline)
        {
            return StorylineState.CompleteMission;
        }
    }
}
