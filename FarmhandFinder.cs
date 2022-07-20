using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using Rectangle = xTile.Dimensions.Rectangle;
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
        
        internal static readonly Dictionary<long, CompassBubble> CompassBubbles = new();

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Instance = this; Config = Helper.ReadConfig<ModConfig>();
            LoadTextures(helper);
            
            // If not all options are disabled, we have work to do.
            if (!Config.HideCompassBubble || !Config.HideCompassArrow)
                helper.Events.Display.RenderedHud += OnRenderedHud;

            if (!Config.HideCompassBubble)
                HandleCompassBubbles(helper);
        }



        /*********
        ** Private methods
        *********/
        /// <summary>Loads the background, foreground, and arrow textures.</summary>
        /// <param name="helper">IModHelper instance for loading data.</param>
        private void LoadTextures(IModHelper helper)
        {
            BackgroundTexture = helper.ModContent.Load<Texture2D>("assets/background2.png");
            ForegroundTexture = helper.ModContent.Load<Texture2D>("assets/foreground.png");
            ArrowTexture = helper.ModContent.Load<Texture2D>("assets/arrow.png");
        }
        
        
        
        /// <summary>Handles the generation and deletion of compass bubble instances.</summary>
        /// <param name="helper">IModHelper instance to add event predicates.</param>
        private void HandleCompassBubbles(IModHelper helper)
        {
            helper.Events.GameLoop.OneSecondUpdateTicked += (_, _) =>
            {
                // Generate a corresponding compass bubble and add to dictionary if one has not been created yet.
                foreach (var peer in Helper.Multiplayer.GetConnectedPlayers())
                {
                    var farmer = Game1.getFarmer(peer.PlayerID);
                    if (CompassBubbles.ContainsKey(farmer.UniqueMultiplayerID))
                        continue;
                    
                    CompassBubbles.Add(farmer.UniqueMultiplayerID, new CompassBubble(farmer, helper));
                }
            };

            // If a peer disconnects from the world, remove their respective dictionary entry.
            // TODO: This doesn't seem to work properly at the moment?
            helper.Events.Multiplayer.PeerDisconnected += (_, e) => CompassBubbles.Remove(e.Peer.PlayerID);

            // If the game returns to the title screen, clear the compass bubble dictionary.
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => CompassBubbles.Clear();
        }
        


        /// <summary>
        /// Raised after drawing the HUD (item toolbar, clock, etc) to the sprite batch, but before it's rendered to
        /// the screen.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
        private void OnRenderedHud(object sender, RenderedHudEventArgs e)
        {
            if (!Context.HasRemotePlayers) return; // Ignore if player hasn't loaded a save yet.

            // TODO: Move intersection calculations into a separate function?
            var playerCenter = Game1.player.Position + new Vector2(0.5f * Game1.tileSize, -0.5f * Game1.tileSize);

            // TODO: check cutsclees!
            
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
                    (int)(farmer.position.X + 0.125f * Game1.tileSize), 
                    (int)(farmer.position.Y - 1.5f * Game1.tileSize), 
                    (int)(0.75f * Game1.tileSize), 2 * Game1.tileSize);
                
                var peerCenter = new Vector2(
                    peerBounds.X + peerBounds.Width / 2f,
                    peerBounds.Y + peerBounds.Height / 2f);
                
                // Skip if bounds and viewport intersect.
                if (peerBounds.Intersects(Game1.viewport)) continue;

                // As we now know that there is a definite intersection between the player, peer, and viewport,
                // calculate the respective intersection point.
                // TODO: Game1.viewport shifts slightly when corrected after changing either the UI scale or zoom level,
                // TODO: however, when using Game1.uiViewport, intersection calculations fail.
                var intersection = Utility.LiangBarskyIntersection(
                    playerCenter, peerCenter, Game1.viewport, Config.HideCompassArrow ? 40 : 50);
                
                // Calculate a normalized position based on the viewport, zoom level, and UI scale.
                var compassPos = (intersection - new Vector2(Game1.viewport.X, Game1.viewport.Y)) 
                    * Game1.options.zoomLevel / Game1.options.uiScale;

                // Only draw the compass bubble if one has already been generated (denoted with the existence of a 
                // farmer head hash).
                if (!Config.HideCompassBubble && CompassBubbles.ContainsKey(farmer.UniqueMultiplayerID))
                {
                    var alpha = Utility.UiElementsIntersect(compassPos) ? 0.5f : 1f;

                    // Drawing the compass bubble at the normalized position.
                    CompassBubbles[farmer.UniqueMultiplayerID].Draw(e.SpriteBatch, compassPos, 1, alpha);
                }

                if (!Config.HideCompassArrow)
                {
                    // Drawing the compass arrow pivoted at an offset in the +X direction about the intersection point
                    // and rotated in the direction of the intersection point to center of the peer.
                    var arrowAngle = (float) Math.Atan2(peerCenter.Y - intersection.Y, peerCenter.X - intersection.X);
                    var arrowPos = compassPos + new Vector2((float)Math.Cos(arrowAngle), (float)Math.Sin(arrowAngle))
                        * (36 * Game1.options.uiScale);
                    Utility.DrawUiSprite(e.SpriteBatch, ArrowTexture, arrowPos, arrowAngle + MathHelper.PiOver2);   
                }
            }
        }
    }
}