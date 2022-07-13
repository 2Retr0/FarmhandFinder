using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace FarmhandFinder
{
    public class FarmhandFinder : Mod
    {
        internal static FarmhandFinder Instance { get; private set; }
        internal static ModConfig Config;
        
        internal static Texture2D BackgroundTexture;
        internal static Texture2D ForegroundTexture;
        internal static Texture2D ArrowTexture;
        
        internal static readonly Dictionary<long, int> FarmerHeadHashes = new();
        internal static readonly Dictionary<long, Texture2D> CompassBubbleTextures = new();

        
        public override void Entry(IModHelper helper)
        {
            Instance = this; Config = Helper.ReadConfig<ModConfig>();
            LoadTextures(helper);
            
            // If not all options are disabled, we have work to do.
            if (!Config.HideCompassBubble || !Config.HideCompassArrow)
                helper.Events.Display.RenderedHud += OnRenderedHud;

            if (!Config.HideCompassBubble)
                HandleCompassBubbleTextureGeneration(helper);
        }



        private void LoadTextures(IModHelper helper)
        {
            BackgroundTexture = helper.ModContent.Load<Texture2D>("assets/background2.png");
            ForegroundTexture = helper.ModContent.Load<Texture2D>("assets/foreground.png");
            ArrowTexture = helper.ModContent.Load<Texture2D>("assets/arrow.png");
        }
        
        
        
        private void HandleCompassBubbleTextureGeneration(IModHelper helper)
        {
            helper.Events.GameLoop.UpdateTicked += (_, e) =>
            {
                if (!e.IsMultipleOf(30)) return; // Only run function every half second.
                
                foreach (var peer in Helper.Multiplayer.GetConnectedPlayers())
                {
                    var farmer = Game1.getFarmer(peer.PlayerID);
                    var currentHeadHash = Utility.GetFarmerHeadHash(farmer);
                    
                    // Only update if the head hash exists and has been changed.
                    if (!FarmerHeadHashes.ContainsKey(farmer.UniqueMultiplayerID)
                        || FarmerHeadHashes[farmer.UniqueMultiplayerID] == currentHeadHash) 
                        continue;

                    // Update farmer compass bubble texture and head hash.
                    CompassBubbleTextures[farmer.UniqueMultiplayerID] = Utility.GenerateCompassBubbleTexture(farmer);
                    FarmerHeadHashes[farmer.UniqueMultiplayerID] = currentHeadHash;
                }
            };

            // If a peer disconnects from the world, remove their respective compass bubble and head hash dictionary
            // entries.
            // TODO: This doesn't seem to work properly at the moment?
            helper.Events.Multiplayer.PeerDisconnected += (_, e) =>
            {
                CompassBubbleTextures.Remove(e.Peer.PlayerID);
                FarmerHeadHashes.Remove(e.Peer.PlayerID);
            };
            
            // If the game returns to the title screen clear the texture and head hash dictionaries.
            helper.Events.GameLoop.ReturnedToTitle += (_, _) =>
            {
                CompassBubbleTextures.Clear();
                FarmerHeadHashes.Clear();
            };
        }


        
        
        private void OnRenderedHud(object sender, RenderedHudEventArgs e)
        {
            if (!Context.HasRemotePlayers) return; // Ignore if player hasn't loaded a save yet.

            // TODO: Game1.viewport shifts slightly when corrected after changing either the UI scale or zoom level,
            // TODO: however, when using Game1.uiViewport, intersection calculations fail.
            // Generate four lines boxing in the viewport with an inwards offset (that is, a smaller 'box').
            var spriteDrawBounds = Utility.GenerateSpriteDrawBounds(
                Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height, 
                Config.HideCompassArrow ? 40 : 50);

            // TODO: Move intersection calculations into a separate function?
            var playerCenter = (Game1.player.Position + new Vector2(0.5f * Game1.tileSize, -0.5f * Game1.tileSize))
                .ToPoint();
            
            foreach (var peer in Helper.Multiplayer.GetConnectedPlayers())
            {
                var farmer = Game1.getFarmer(peer.PlayerID);

                // TODO: More split screen checks are needed--specifically having the remote peer bubble show up in both
                // TODO: screens and the remote peer bubble needs to be displayed on the correct location for each screen.
                // Skip if the selected farmer is a remote peer and their current location is the same as the player.
                if (peer.IsSplitScreen 
                    || farmer.currentLocation == null 
                    || !farmer.currentLocation.Equals(Game1.player.currentLocation)) 
                    continue;

                // We denote an approximate bound about the farmer before checking if the approximate bound intersects
                // the viewport--if so, the peer is on screen and should be skipped. If not, then a line drawn between
                // the player and peer definitely intersects the viewport draw bounds.
                // TODO: Maybe use farmer.getBoundingBox() in some way rather than make our own?
                var peerBounds = new Rectangle(
                    (int)(farmer.position.X + 0.125f * Game1.tileSize), (int)(farmer.position.Y - 1.5f * Game1.tileSize), 
                    (int)(0.75f * Game1.tileSize), 2 * Game1.tileSize);
                var peerCenter = peerBounds.Center;
                // Skip if bounds and viewport intersect.
                if (peerBounds.Intersects(Game1.viewport.ToXna())) continue;

                // We find the first non-null intersection point between a line in spriteDrawBounds and a line between
                // the centers of the player and peer.
                var intersection = (Vector2) spriteDrawBounds.Select(l =>
                    Utility.LineIntersect(playerCenter, peerBounds.Center, l.Item1, l.Item2)).First(p => p != null);

                // Calculate a normalized position based on the viewport, zoom level, and UI scale.
                var backgroundPos = (intersection - new Vector2(Game1.viewport.X, Game1.viewport.Y)) 
                    * Game1.options.zoomLevel / Game1.options.uiScale;

                if (!Config.HideCompassBubble)
                {
                    // Generate a corresponding compass bubble texture and head hash for the farmer if one has not been
                    // created yet.
                    // TODO: This should be made independent of when the screen draws however when trying to use events
                    // TODO: such as 'GameLoop.SaveLoaded', peers' baseTexture's remain null and cannot be drawn.
                    // TODO: A similar issue occurs with 'Multiplayer.PeerConnected'.
                    if (!FarmerHeadHashes.ContainsKey(farmer.UniqueMultiplayerID))
                    {
                        CompassBubbleTextures.Add(farmer.UniqueMultiplayerID, Utility.GenerateCompassBubbleTexture(farmer));
                        FarmerHeadHashes.Add(farmer.UniqueMultiplayerID, Utility.GetFarmerHeadHash(farmer));
                    }
                    
                    // Drawing the compass bubble at the normalized position.
                    Utility.DrawCompassBubbleTexture(e.SpriteBatch, farmer, backgroundPos, 1, 0.5f);
                }

                if (!Config.HideCompassArrow)
                {
                    // Drawing the compass arrow pivoted at an offset in the +X direction about the intersection point
                    // and rotated in the direction of the intersection point to center of the peer.
                    var arrowAngle = (float) Math.Atan2(peerCenter.Y - intersection.Y, peerCenter.X - intersection.X);
                    var arrowPos = backgroundPos + new Vector2((float)Math.Cos(arrowAngle), (float)Math.Sin(arrowAngle))
                        * (36 * Game1.options.uiScale);
                    Utility.DrawUiSprite(e.SpriteBatch, ArrowTexture, arrowPos, arrowAngle + MathHelper.PiOver2);   
                }
            }
        }
    }
}