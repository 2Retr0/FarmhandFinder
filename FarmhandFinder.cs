using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.HomeRenovations;

namespace FarmhandFinder
{ 
    public class FarmhandFinder : Mod
    {
        private readonly Texture2D _texture = new (Game1.graphics.GraphicsDevice, 1, 1);
        private Tuple<Point, Point>[] _spriteDrawBounds;

        public override void Entry(IModHelper helper)
        {
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
            helper.Events.World.LocationListChanged += OnLocationListChanged;
            helper.Events.Display.WindowResized += OnWindowResized;
            _texture.SetData(new[] { Color.White });
            _spriteDrawBounds = GenerateSpriteDrawBounds(
                Game1.viewport.Location.X, Game1.viewport.Location.Y, Game1.viewport.Width, Game1.viewport.Height, 20);
        }
        
        
        
        private static Tuple<Point, Point>[] GenerateSpriteDrawBounds(int x, int y, int w, int h, int offset)
        {
            var p1 = new Point(x,   y)   + new Point(offset,  offset);
            var p2 = new Point(x,   y+h) + new Point(offset,  -offset);
            var p3 = new Point(x+w, y+h) + new Point(-offset, -offset);
            var p4 = new Point(x+w, y)   + new Point(-offset, offset);

            return new Tuple<Point, Point>[] { new(p1, p4), new(p2, p3), new(p1, p2), new(p3, p4) };
        }



        private void OnLocationListChanged(object sender, LocationListChangedEventArgs e)
        {
            Monitor.Log("Added: " + e.Added.GetEnumerator().Current + ", Removed: " + e.Removed.GetEnumerator().Current, LogLevel.Info);
        }



        private void OnWindowResized(object sender, WindowResizedEventArgs e)
        {
            _spriteDrawBounds = GenerateSpriteDrawBounds(
                Game1.viewport.Location.X, Game1.viewport.Location.Y, e.NewSize.X, e.NewSize.Y, 20);
        }



        private void DrawPlayerHead(SpriteBatch spriteBatch)
        {
            // TODO: Zoom level affects sprite size?
            var baseTexture = Helper.Reflection.GetField<Texture2D>(Game1.player.FarmerRenderer, "baseTexture").GetValue();
            //var hairstyleSourceRect = Helper.Reflection.GetField<Rectangle>(Game1.player.FarmerRenderer, "hairstyleSourceRect");

            
            //Game1.player.FarmerRenderer.draw(e.SpriteBatch, Game1.player.FarmerSprite.CurrentAnimationFrame, 0, Game1.player.FarmerSprite.sourceRect,
              //  Vector2.One * 100, Vector2.Zero, 0.8f, 2, Color.White, 0f, 1f, Game1.player); //blinks???
            
              
            Game1.player.FarmerSprite.setCurrentSingleFrame(0);
            spriteBatch.Draw(baseTexture, 250 * Vector2.One, Game1.player.FarmerSprite.sourceRect, Color.White, 0f, Vector2.Zero, 4f * 1, SpriteEffects.None, 0.8f);
            
            
            var hairStyleMetadata = Farmer.GetHairStyleMetadata(Game1.player.hair.Value);
            var texture = FarmerRenderer.hairStylesTexture;
            if (hairStyleMetadata != null) {
                texture = hairStyleMetadata.texture;
            }
            
            var hair = Game1.player.getHair();
            var hairstyleSourceRect = new Rectangle(hair * 16 % FarmerRenderer.hairStylesTexture.Width, hair * 16 / FarmerRenderer.hairStylesTexture.Width * 96, 16, 15);
            if (hairStyleMetadata != null)
            {
                texture = hairStyleMetadata.texture;
                hairstyleSourceRect = new Rectangle(hairStyleMetadata.tileX * 16, hairStyleMetadata.tileY * 16, 16, 15);
            }
            
            spriteBatch.Draw(baseTexture, 100 * Vector2.One, new Rectangle(0, 0, 16, Game1.player.IsMale ? 15 : 16), Color.White, 0.0f, Vector2.Zero, 4 * 1, SpriteEffects.None, 0.8f);
            var num2 = Game1.isUsingBackToFrontSorting ? -1 : 1;
            
            spriteBatch.Draw(texture, 100 * Vector2.One + new Vector2(0.0f, (FarmerRenderer.featureYOffsetPerFrame[0] * 4 + (!Game1.player.IsMale || Game1.player.hair.Value < 16 ? (Game1.player.IsMale || Game1.player.hair.Value >= 16 ? 0 : 4) : -4))) * 1, hairstyleSourceRect, Game1.player.hairstyleColor.Value, 0.0f, Vector2.Zero, 4 * 1, SpriteEffects.None, 0.8f + 1.1E-07f * num2);
            if (Game1.player.hat.Value != null)
            {
                var hatSourceRect =
                    new Rectangle(20 * Game1.player.hat.Value.which.Value % FarmerRenderer.hatsTexture.Width,
                        20 * Game1.player.hat.Value.which.Value / FarmerRenderer.hatsTexture.Width * 20 * 4 + 4, 20, 20 - 4);
                
                spriteBatch.Draw(FarmerRenderer.hatsTexture,
                    100 * Vector2.One + new Vector2((FarmerRenderer.featureXOffsetPerFrame[0] * 4 - 8),
                        (FarmerRenderer.featureYOffsetPerFrame[0] * 4 - 16 +
                         (Game1.player.hat.Value.ignoreHairstyleOffset.Value
                             ? 0
                             : FarmerRenderer.hairstyleHatOffset[Game1.player.hair.Value % 16]) + 4)) + Vector2.UnitY * (4 * 4), hatSourceRect,
                    Game1.player.hat.Value.isPrismatic.Value ? Utility.GetPrismaticColor() : Color.White, 0f,
                    Vector2.Zero, 4f * 1, SpriteEffects.None, 0.8f);
            }
        }



        [SuppressMessage("ReSharper", "UseDeconstructionOnParameter")]
        [SuppressMessage("ReSharper", "PossibleUnintendedReferenceComparison")]
        private void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            // TODO: REMOVE THIS LATER
            // if (!Context.HasRemotePlayers) return; // Ignore if player hasn't loaded a save yet.

            void DrawLine(Vector2 p1, Vector2 p2)
            {
                p1 -= new Vector2(Game1.viewport.X, Game1.viewport.Y);
                p2 -= new Vector2(Game1.viewport.X, Game1.viewport.Y);

                e.SpriteBatch.Draw(_texture, p1, null, Color.Red, 
                    (float) Math.Atan2(p2.Y - p1.Y, p2.X - p1.X), Vector2.Zero, 
                    new Vector2(Vector2.Distance(p1, p2), 2f), SpriteEffects.None, 0f);
            }

            // Algorithm adapted from Paul Bourke http://paulbourke.net/geometry/pointlineplane/
            Vector2? LineIntersect(Point p1, Point p2, Point p3, Point p4)
            {
                // if (p1 == p2 || p3 == p4) return null;
                
                var d = (float) ((p4.Y - p3.Y) * (p2.X - p1.X) - (p4.X - p3.X) * (p2.Y - p1.Y));
                var ua = ((p4.X - p3.X) * (p1.Y - p3.Y) - (p4.Y - p3.Y) * (p1.X - p3.X)) / d;
                var ub = ((p2.X - p1.X) * (p1.Y - p3.Y) - (p2.Y - p1.Y) * (p1.X - p3.X)) / d;

                if (ua is < 0 or > 1 || ub is < 0 or > 1) return null;
                
                return new Vector2(p1.X + ua * (p2.X - p1.X), p1.Y + ua * (p2.Y - p1.Y));
            }

            Tuple<Point, Point>[] GenerateSpriteDrawBounds(int x, int y, int w, int h, int offset)
            {
                var p1 = new Point(x,   y)   + new Point(offset,  offset);
                var p2 = new Point(x,   y+h) + new Point(offset,  -offset);
                var p3 = new Point(x+w, y+h) + new Point(-offset, -offset);
                var p4 = new Point(x+w, y)   + new Point(-offset, offset);

                return new Tuple<Point, Point>[] { new(p1, p4), new(p2, p3), new(p1, p2), new(p3, p4) };
            }

            void DrawRectangle(Rectangle rect)
            {
                DrawLine(new Vector2(rect.X, rect.Y), new Vector2(rect.X + rect.Width, rect.Y));
                DrawLine(new Vector2(rect.X + rect.Width, rect.Y), new Vector2(rect.X + rect.Width, rect.Y + rect.Height));
                DrawLine(new Vector2(rect.X, rect.Y + rect.Height), new Vector2(rect.X + rect.Width, rect.Y + rect.Height));
                DrawLine(new Vector2(rect.X, rect.Y), new Vector2(rect.X, rect.Y + rect.Height));
            }            
            var playerRect = new Rectangle((int) (Game1.player.position.X + 0.125f * Game1.tileSize), 
                                           (int) (Game1.player.position.Y - 1.5f * Game1.tileSize), 
                                           (int) (0.75f * Game1.tileSize), 2 * Game1.tileSize);
            DrawRectangle(playerRect);
            
            var viewportRect = new Rectangle(_spriteDrawBounds[0].Item1.X, _spriteDrawBounds[0].Item1.Y,
                _spriteDrawBounds[0].Item2.X - _spriteDrawBounds[0].Item1.X, 
                _spriteDrawBounds[2].Item2.Y - _spriteDrawBounds[2].Item1.Y);
            DrawRectangle(viewportRect);

            var playerCenter = (Game1.player.Position + new Vector2(0.5f * Game1.tileSize, -0.5f * Game1.tileSize)).ToPoint();


            // Maybe cache players?
            foreach (var farmer in Game1.getOnlineFarmers())
            {
                var peer = Helper.Multiplayer.GetConnectedPlayer(farmer.UniqueMultiplayerID);

                // TODO: more split screen checks are needed--specifically having the remote peer bubble show up in both screens
                // TODO: and the remote peer bubble needs to be displayed on the correct location for each screen.
                // If the selected farmer is a remote peer...
                if (farmer == Game1.player || peer.IsSplitScreen || Game1.player.currentLocation != farmer.currentLocation) continue;

                // Approximate bounds around a player (i.e. Farmer).
                var peerBounds = new Rectangle((int) (farmer.Position.X + 0.125f * Game1.tileSize), 
                                               (int) (farmer.Position.Y - 1.5f * Game1.tileSize), 
                                               (int) (0.75f * Game1.tileSize), 2 * Game1.tileSize);
                if (peerBounds.Intersects(Game1.viewport.ToXna())) continue;

                // Definitely intersects
                var spriteDrawIntersection = _spriteDrawBounds.Select(l => 
                    LineIntersect(playerCenter, peerBounds.Center, l.Item1, l.Item2)).First(p => p != null);

                DrawLine(playerCenter.ToVector2(), (Vector2) spriteDrawIntersection);
            }
        }
    }
}