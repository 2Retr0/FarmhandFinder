using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewValley;

namespace FarmhandFinder
{
    public static class Utility
    {
        private static readonly Texture2D _texture = new (Game1.graphics.GraphicsDevice, 1, 1);

        public static Tuple<Point, Point>[] GenerateSpriteDrawBounds(int x, int y, int w, int h, int offset)
        {
            var o = (int) (offset * Game1.options.uiScale / Game1.options.zoomLevel);
            
            var p1 = new Point(x,   y)   + new Point(o,   o);
            var p2 = new Point(x,   y+h) + new Point(o,  -o);
            var p3 = new Point(x+w, y+h) + new Point(-o, -o);
            var p4 = new Point(x+w, y)   + new Point(-o,  o);

            return new Tuple<Point, Point>[] { new(p1, p4), new(p2, p3), new(p1, p2), new(p3, p4) };
        }
        
        
        
        public static void DrawLine(SpriteBatch spriteBatch, Vector2 p1, Vector2 p2)
        {
            p1 -= (Game1.options.zoomLevel / Game1.options.uiScale) * new Vector2(Game1.viewport.X, Game1.viewport.Y);
            p2 -= (Game1.options.zoomLevel / Game1.options.uiScale) * new Vector2(Game1.viewport.X, Game1.viewport.Y);

            _texture.SetData(new[] { Color.White });
            spriteBatch.Draw(_texture, p1, null, Color.Red, 
                (float) Math.Atan2(p2.Y - p1.Y, p2.X - p1.X), Vector2.Zero, 
                new Vector2(Vector2.Distance(p1, p2), 2f), SpriteEffects.None, 0f);
        }
        
        
        
        // Algorithm adapted from Paul Bourke http://paulbourke.net/geometry/pointlineplane/
        public static Vector2? LineIntersect(Point p1, Point p2, Point p3, Point p4)
        {
            var d = (float) ((p4.Y - p3.Y) * (p2.X - p1.X) - (p4.X - p3.X) * (p2.Y - p1.Y));
            var ua = ((p4.X - p3.X) * (p1.Y - p3.Y) - (p4.Y - p3.Y) * (p1.X - p3.X)) / d;
            var ub = ((p2.X - p1.X) * (p1.Y - p3.Y) - (p2.Y - p1.Y) * (p1.X - p3.X)) / d;

            if (ua is < 0 or > 1 || ub is < 0 or > 1) return null;
                
            return new Vector2(p1.X + ua * (p2.X - p1.X), p1.Y + ua * (p2.Y - p1.Y));
        }

        
        
        // TODO: Pixel size is ~1.5x larger than that of other UI elements at high zoom levels (150%)?
        public static void DrawUiSprite(SpriteBatch spriteBatch, Texture2D texture, Vector2 position, float angle)
        {
            int width = texture.Width, height = texture.Height;
            spriteBatch.Draw(
                texture, position, new Rectangle(0, 0, width, height), Color.White, angle, 
                new Vector2(width / 2f, height / 2f), 4 * Game1.options.uiScale, SpriteEffects.None, 0.8f);
        }
        
        
        
        public static void DrawRectangle(SpriteBatch spriteBatch, Rectangle r)
        {
            DrawLine(spriteBatch, new Vector2(r.X, r.Y),            new Vector2(r.X + r.Width, r.Y));
            DrawLine(spriteBatch, new Vector2(r.X + r.Width, r.Y),  new Vector2(r.X + r.Width, r.Y + r.Height));
            DrawLine(spriteBatch, new Vector2(r.X, r.Y + r.Height), new Vector2(r.X + r.Width, r.Y + r.Height));
            DrawLine(spriteBatch, new Vector2(r.X, r.Y),            new Vector2(r.X, r.Y + r.Height));
        }
        
        
        
        public static void DrawFarmerHead(SpriteBatch spriteBatch, Farmer farmer, Vector2 position, float scale)
        {
            // The constants for the origin are chosen such that the head lines up just above the third-most bottom
            // pixel of the background sprite. I'm not sure how to make the calculation cleaner...
            var origin = new Vector2(8f, (1 + scale) / 2 * (10.25f + (farmer.IsMale ? 0 : 0.75f)));

            void DrawFarmerFace()
            {
                // Get the base texture of the target farmer--it will include the skin color, eye color, etc.
                var baseTexture = FarmhandFinder.Instance.Helper.Reflection.GetField<Texture2D>(
                    farmer.FarmerRenderer, "baseTexture").GetValue();
                // headSourceRect corresponds to the front facing sprite.
                var headSourceRect = new Rectangle(0, 0, 16, farmer.IsMale ? 15 : 16);

                spriteBatch.Draw(
                    baseTexture, 
                    position, 
                    headSourceRect, 
                    Color.White, 
                    0, origin, 4 * scale, SpriteEffects.None, 0.8f);
            }
            
            void DrawFarmerAccessories()
            {
                var accessorySourceRect = new Rectangle(
                    farmer.accessory.Value * 16 % FarmerRenderer.accessoriesTexture.Width,
                    farmer.accessory.Value * 16 / FarmerRenderer.accessoriesTexture.Width * 32, 
                    16, 16);
                
                spriteBatch.Draw(
                    FarmerRenderer.accessoriesTexture,
                    position + new Vector2(0, 8) * scale,
                    accessorySourceRect, 
                    farmer.hairstyleColor.Value,
                    0, origin, 4 * scale, SpriteEffects.None, 0.8f + farmer.accessory.Value < 8 ? 
                        1.9E-05f : 2.9E-05f);
            }

            void DrawFarmerHair(int bottomOffset)
            {
                var hairIndex = farmer.getHair();
                var hairStyleMetadata = Farmer.GetHairStyleMetadata(farmer.hair.Value);
                
                // Logic for hair shown beneath a hat.
                if (farmer.hat.Value != null && farmer.hat.Value.hairDrawType.Value == 1 && 
                    hairStyleMetadata != null && hairStyleMetadata.coveredIndex != -1)
                {
                    hairIndex = hairStyleMetadata.coveredIndex;
                    hairStyleMetadata = Farmer.GetHairStyleMetadata(hairIndex);
                }
                
                var hairstyleTexture = FarmerRenderer.hairStylesTexture;
                // We factor in an 'offset' to ensure that the hair does not clip outside of the background.
                var hairstyleSourceRect = new Rectangle(
                    hairIndex * 16 % FarmerRenderer.hairStylesTexture.Width, 
                    hairIndex * 16 / FarmerRenderer.hairStylesTexture.Width * 96, 
                    16, 32 - bottomOffset);
                
                if (hairStyleMetadata != null)
                {
                    hairstyleTexture = hairStyleMetadata.texture;
                    hairstyleSourceRect = new Rectangle(
                        hairStyleMetadata.tileX * 16, hairStyleMetadata.tileY * 16, 
                        16, 32 - bottomOffset);
                }

                spriteBatch.Draw(
                    hairstyleTexture, 
                    position + new Vector2(
                        0, 
                        4 + (!farmer.IsMale || farmer.hair.Value < 16 ? 
                            (farmer.IsMale || farmer.hair.Value >= 16 ? 0 : 4) : -4)) * scale, 
                    hairstyleSourceRect, 
                    farmer.hairstyleColor.Value, 
                    0, origin, 4 * scale, SpriteEffects.None, 0.8f + 2.2E-05f);
                }
            
            void DrawFarmerHat(int sideOffset, int topOffset, int bottomOffset)
            {
                var hatOrigin = origin - new Vector2(0, farmer.IsMale ? 0 : 1);
                // We factor in an 'offset' to ensure that the hair does not clip outside of the background.
                var hatSourceRect = new Rectangle(
                    20 * farmer.hat.Value.which.Value % FarmerRenderer.hatsTexture.Width + sideOffset,
                    20 * farmer.hat.Value.which.Value / FarmerRenderer.hatsTexture.Width * 20 * 4 + topOffset, 
                    20 - 2 * sideOffset, 20 - topOffset - bottomOffset);
                
                spriteBatch.Draw(
                    FarmerRenderer.hatsTexture, 
                    position + new Vector2(
                        -8 + 4 * sideOffset, 
                        -12 + 4 * topOffset + (farmer.hat.Value.ignoreHairstyleOffset.Value ? 
                            0 : FarmerRenderer.hairstyleHatOffset[farmer.hair.Value % 16]) + 4) * scale, 
                    hatSourceRect, 
                    farmer.hat.Value.isPrismatic.Value ? StardewValley.Utility.GetPrismaticColor() : Color.White, 
                    0, hatOrigin, 4 * scale, SpriteEffects.None, 0.8f + 3.9E-05f);
            }

            // Note: Accessories are facial hair and glasses.
            DrawFarmerFace();
            if (farmer.accessory.Value >= 0) 
                DrawFarmerAccessories();
            
            // Offset values are chosen roughly on what can fit with a 0.75 scale value--they must be manually changed
            // otherwise.
            DrawFarmerHair(16);
            // Note: A decision was made not to render the farmer's 'normal' hairstyle when the farmer is in a bathing
            //       suit as it may be confusing for identifying the farmer.
            if (farmer.hat.Value != null) 
                DrawFarmerHat(2, 3, 2);
        
        }
        
        
        
        public static Texture2D GenerateCompassBubbleTexture(Farmer farmer)
        {
            var graphicsDevice = Game1.graphics.GraphicsDevice;
            var backgroundTexture = FarmhandFinder.BackgroundTexture; 
            var foregroundTexture = FarmhandFinder.ForegroundTexture;
            
            // We first generate a new RenderTarget2D which will act as a buffer to hold the final texture.
            // Since the scaling of the sprites will already be at 400% (as we want to draw the farmer head at a scale
            // different from the background and foreground), we have to make the texture at a scale of 400% of the
            // true texture dimensions.
            var textureBuffer = new RenderTarget2D(
                graphicsDevice, 
                4 * backgroundTexture.Width, 
                4 * backgroundTexture.Height, 
                false, SurfaceFormat.Color, DepthFormat.None);

            // We then change the graphics device to target the new texture buffer such that when we draw sprites, they
            // will be drawn onto the texture rather than to the screen.
            graphicsDevice.SetRenderTarget(textureBuffer);
            graphicsDevice.Clear(Color.Transparent);

            // We then create a temporary spriteBatch to which we draw the background, farmer head, and foreground at
            // an offset equal to the middle of the final texture.
            var offset = 4 * backgroundTexture.Width / 2f * Vector2.One;
            using (var spriteBatch = new SpriteBatch(graphicsDevice))
            {
                spriteBatch.Begin(
                    samplerState: SamplerState.PointClamp,
                    depthStencilState: DepthStencilState.Default, 
                    rasterizerState: RasterizerState.CullNone);

                DrawUiSprite(spriteBatch, backgroundTexture, offset, 0f);
                DrawFarmerHead(spriteBatch, farmer, offset, 0.75f);
                DrawUiSprite(spriteBatch, foregroundTexture, offset, 0f);

                spriteBatch.End();
            }
            
            // Finally, the render target of the graphics device is reset and we return the resulting texture drawn
            // to the temporary spriteBatch.
            graphicsDevice.SetRenderTarget(null);
            return textureBuffer;
        }



        public static void DrawCompassBubbleTexture(
            SpriteBatch spriteBatch, Farmer farmer, Vector2 position, float scale, float alpha)
        {
            if (!FarmhandFinder.CompassBubbleTextures.ContainsKey(farmer.UniqueMultiplayerID))
                return;

            var compassTexture = FarmhandFinder.CompassBubbleTextures[farmer.UniqueMultiplayerID];
            int width = compassTexture.Width, height = compassTexture.Height;
            spriteBatch.Draw(
                compassTexture, position, new Rectangle(0, 0, width, height), Color.White * alpha, 0, 
                new Vector2(width / 2f, height / 2f), scale * Game1.options.uiScale, SpriteEffects.None, 0.8f);
        }
        
        
        
        public static int GetFarmerHeadHash(Farmer farmer)
        {
            // TODO: Is there a better way to check the base texture for farmers (i.e. nose, gender, etc.)?
            var textureNameValue = FarmhandFinder.Instance.Helper.Reflection.GetField<NetString>(
                    farmer.FarmerRenderer, "textureName").GetValue();

            // We hash a combination of various features that can be changed on the farmer's head for easy comparison.
            return HashCode.Combine(
                textureNameValue, farmer.newEyeColor, farmer.skin, farmer.hair, farmer.hairstyleColor, farmer.hat);
        } 
    }
}