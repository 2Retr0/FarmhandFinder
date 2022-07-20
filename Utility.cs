using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace FarmhandFinder
{
    public static class Utility
    {
        private static readonly Texture2D _texture = new (Game1.graphics.GraphicsDevice, 1, 1);



        public static void DrawLine(SpriteBatch spriteBatch, Vector2 p1, Vector2 p2)
        {
            // p1 -= (Game1.options.zoomLevel / Game1.options.uiScale) * new Vector2(Game1.viewport.X, Game1.viewport.Y);
            // p2 -= (Game1.options.zoomLevel / Game1.options.uiScale) * new Vector2(Game1.viewport.X, Game1.viewport.Y);

            _texture.SetData(new[] { Color.White });
            spriteBatch.Draw(_texture, p1, null, Color.Red, 
                (float) Math.Atan2(p2.Y - p1.Y, p2.X - p1.X), Vector2.Zero, 
                new Vector2(Vector2.Distance(p1, p2), 2f), SpriteEffects.None, 0f);
        }



        public static void DrawRectangle(SpriteBatch spriteBatch, Rectangle r)
        {
            DrawLine(spriteBatch, new Vector2(r.X, r.Y),            new Vector2(r.X + r.Width, r.Y));
            DrawLine(spriteBatch, new Vector2(r.X + r.Width, r.Y),  new Vector2(r.X + r.Width, r.Y + r.Height));
            DrawLine(spriteBatch, new Vector2(r.X, r.Y + r.Height), new Vector2(r.X + r.Width, r.Y + r.Height));
            DrawLine(spriteBatch, new Vector2(r.X, r.Y),            new Vector2(r.X, r.Y + r.Height));
        }

        
        
        // TODO: Pixel size is ~1.5x larger than that of other UI elements at high zoom levels (150%)?
        public static void DrawUiSprite(SpriteBatch spriteBatch, Texture2D texture, Vector2 position, float angle)
        {
            int width = texture.Width, height = texture.Height;
            spriteBatch.Draw(
                texture, position, new Rectangle(0, 0, width, height), Color.White, angle, 
                new Vector2(width / 2f, height / 2f), 4 * Game1.options.uiScale, SpriteEffects.None, 0.8f);
        }



        public static void DrawFarmerHead(SpriteBatch spriteBatch, Farmer farmer, Vector2 position, float scale)
        {
            // The constants for the origin are chosen such that the head lines up just above the third-most bottom
            // pixel of the background sprite. I'm not sure how to make the calculation cleaner...
            var origin = new Vector2(8f, (1 + scale) / 2 * (10.25f + (farmer.IsMale ? 0 : 0.75f)));
            
            // Get the base texture of the target farmer--it will include the skin color, eye color, etc.
            var baseTexture = FarmhandFinder.Instance.Helper.Reflection.GetField<Texture2D>(
                farmer.FarmerRenderer, "baseTexture").GetValue();

            void DrawFarmerFace()
            {
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

            // Only draw the head if the base texture has been generated.
            if (baseTexture == null) return;
            
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



        public static Vector2 LiangBarsky(Vector2 p1, Vector2 p2, xTile.Dimensions.Rectangle r, int offset)
        {
            float t = 1f, o = offset * Game1.options.uiScale / Game1.options.zoomLevel;
            float minX = r.X + o, 
                  minY = r.Y + o, 
                  maxX = r.X + r.Width - o, 
                  maxY = r.Y + r.Height - o;
            
            // Iterate over the sides of the rectangle.
            float dx = p2.X - p1.X, dy = p2.Y - p1.Y;
            for (var i = 0; i < 4; i++) 
            {
                float p = 0f, q = 0f;
                switch (i)
                {
                    case 0: p = -dx; q = p1.X - minX; break; // Left side.
                    case 1: p =  dx; q = maxX - p1.X; break; // Bottom side.
                    case 2: p = -dy; q = p1.Y - minY; break; // Right side.
                    case 3: p =  dy; q = maxY - p1.Y; break; // Top side.
                }

                var u = q / p;
                if (p > 0 && u < t) t = u;
            }
            
            return new Vector2(p1.X + t * dx, p1.Y + t * dy);
        }
    }
}