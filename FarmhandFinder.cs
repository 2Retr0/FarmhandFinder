using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Network;
using Color = Microsoft.Xna.Framework.Color;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace FarmhandFinder
{
    public class FarmhandFinder : Mod
    {
        internal static FarmhandFinder Instance { get; private set; }

        private Texture2D backgroundTexture;
        private Texture2D foregroundTexture;
        private Texture2D arrowTexture;

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            
            helper.Events.Display.RenderedHud += OnRenderedHud;
            
            backgroundTexture = helper.ModContent.Load<Texture2D>("assets/background2.png");
            foregroundTexture = helper.ModContent.Load<Texture2D>("assets/foreground.png");
            arrowTexture = helper.ModContent.Load<Texture2D>("assets/arrow.png");
        }

        private void OnRenderedHud(object sender, RenderedHudEventArgs e)
        {
            if (!Context.HasRemotePlayers) return; // Ignore if player hasn't loaded a save yet.

            // TODO: Game1.viewport shifts slightly when corrected after changing either the UI scale or zoom level,
            // TODO: however, when using Game1.uiViewport, intersection calculations fail.
            // Generate four lines boxing in the viewport with an inwards offset (that is a smaller 'box').
            var spriteDrawBounds = Utility.GenerateSpriteDrawBounds(
                Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height, 50);

            var playerCenter = (Game1.player.Position + new Vector2(0.5f * Game1.tileSize, -0.5f * Game1.tileSize)).ToPoint();

            // TODO: Maybe cache players?
            foreach (var farmer in Game1.getOnlineFarmers())
            {
                var peer = Helper.Multiplayer.GetConnectedPlayer(farmer.UniqueMultiplayerID);

                // TODO: More split screen checks are needed--specifically having the remote peer bubble show up in both
                // TODO: screens and the remote peer bubble needs to be displayed on the correct location for each screen.
                // If the selected farmer is a remote peer...
                if (farmer == Game1.player || peer.IsSplitScreen ||
                    !Game1.player.currentLocation.Equals(farmer.currentLocation)) continue;

                // Approximate bounds about a peer (i.e. Farmer). We then check if the approximate bounds intersects
                // the viewport--if so, the peer is on screen and can be skipped.
                var peerBounds = new Rectangle(
                    (int)(farmer.position.X + 0.125f * Game1.tileSize), (int)(farmer.position.Y - 1.5f * Game1.tileSize), 
                    (int)(0.75f * Game1.tileSize), 2 * Game1.tileSize);
                var peerCenter = peerBounds.Center;
                // If this fails, a line drawn between the player and peer must intersect the viewport.
                if (peerBounds.Intersects(Game1.viewport.ToXna())) continue;
                
                // We find the first non-null intersection point between a line in spriteDrawBounds and a
                // line between the centers of the player and peer.
                var intersection = (Vector2) spriteDrawBounds.Select(l =>
                    Utility.LineIntersect(playerCenter, peerBounds.Center, l.Item1, l.Item2)).First(p => p != null);

                // Drawing the background sprite, farmer head, and foreground sprite at the intersection point.
                var backgroundPos = (intersection - new Vector2(Game1.viewport.X, Game1.viewport.Y)) 
                    * Game1.options.zoomLevel / Game1.options.uiScale;
                Utility.DrawUiSprite(e.SpriteBatch, backgroundTexture, backgroundPos, 0f);
                Utility.DrawFarmerHead(e.SpriteBatch, farmer, backgroundPos, 0.75f);
                Utility.DrawUiSprite(e.SpriteBatch, foregroundTexture, backgroundPos, 0f);

                // Drawing the arrow sprite pivoted at an offset in the +X direction about the intersection point and
                // rotated in the direction of the intersection point to center of the peer.
                var arrowAngle = (float) Math.Atan2(peerCenter.Y - intersection.Y, peerCenter.X - intersection.X);
                var arrowPos = backgroundPos + new Vector2((float)Math.Cos(arrowAngle), (float)Math.Sin(arrowAngle))
                    * (36 * Game1.options.uiScale);
                Utility.DrawUiSprite(e.SpriteBatch, arrowTexture, arrowPos, arrowAngle + MathHelper.PiOver2);
            }
        }
    }
}