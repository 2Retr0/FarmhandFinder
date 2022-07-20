using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace FarmhandFinder
{
    public class CompassBubble
    {
        private readonly Farmer farmer;
        private int farmerHeadHash = -1;
        private Texture2D compassTexture;



        private int GenerateHeadHash()
        {
            // TODO: Is there a better way to check the base texture for farmers (i.e. nose, gender, etc.)?
            var baseTextureValue = FarmhandFinder.Instance.Helper.Reflection.GetField<Texture2D>(
                farmer.FarmerRenderer, "baseTexture").GetValue();
            if (baseTextureValue == null) return -1;

            // We hash a combination of various features that can be changed on the farmer's head for easy comparison.
            var headHash = HashCode.Combine(
                baseTextureValue, farmer.newEyeColor, farmer.skin, farmer.hair, farmer.hairstyleColor, farmer.hat);
            
            // Ensure that a hash value of -1 is reserved for null base texture values only.
            return headHash == -1 ? ++headHash : headHash;
        } 
        
        
        
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!e.IsMultipleOf(30)) return; // Only run function every half second.
            
            // Regenerate the compass texture if the head hash has changed and is non-null.
            var currentHeadHash = GenerateHeadHash();
            if (currentHeadHash == farmerHeadHash || currentHeadHash == -1) return;
            
            farmerHeadHash = currentHeadHash;
            compassTexture = GenerateTexture();
        }
        
        
        
        public CompassBubble(Farmer targetFarmer, IModHelper helper)
        {
            farmer = targetFarmer;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }



        public Texture2D GenerateTexture()
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

                Utility.DrawUiSprite(spriteBatch, backgroundTexture, offset, 0f);
                Utility.DrawFarmerHead(spriteBatch, farmer, offset, 0.75f);
                Utility.DrawUiSprite(spriteBatch, foregroundTexture, offset, 0f);

                spriteBatch.End();
            }
            
            // Finally, the render target of the graphics device is reset and we save the resulting texture.
            graphicsDevice.SetRenderTarget(null);
            return textureBuffer;
        }



        public void Draw(SpriteBatch spriteBatch, Vector2 position, float scale, float alpha)
        {
            // Only draw if the farmer's head hash is non-null (i.e. their head texture has been generated).
            if (farmerHeadHash == -1) return;

            int width = compassTexture.Width, height = compassTexture.Height;
            spriteBatch.Draw(
                compassTexture, position, new Rectangle(0, 0, width, height), Color.White * alpha, 0, 
                new Vector2(width / 2f, height / 2f), scale * Game1.options.uiScale, SpriteEffects.None, 0.8f);
        }
    }
}