using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace FarmhandFinder
{
    public class CompassBubble
    {
        private const float ALPHA_DELTA_TIME = 300f;

        private readonly Farmer farmer;
        private int farmerHeadHash = -1;
        private Texture2D compassTexture;
        private SmoothLerpUtil alphaLerpUtil;
        private float currentAlpha = 1f;

        /*********
        ** Public methods
        *********/
        /// <summary>Creates a new compass bubble for a farmer.</summary>
        /// <param name="targetFarmer">The farmer which the compass bubble will represent.</param>
        /// <param name="helper">IModHelper instance to add event predicates.</param>
        public CompassBubble(Farmer targetFarmer, IModHelper helper)
        {
            farmer = targetFarmer;
            alphaLerpUtil = new SmoothLerpUtil(1f, 1f, ALPHA_DELTA_TIME);

            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }



        /// <summary>Draws the compass bubble.</summary>
        /// <param name="spriteBatch">The spritebatch to draw to.</param>
        /// <param name="position">The position at which the sprite will be drawn. The sprite will be centered about
        /// this position.</param>
        /// <param name="scale">The scale at which the sprite will be drawn.</param>
        /// <param name="alpha">The alpha value at which the sprite will be drawn.</param>
        public void Draw(SpriteBatch spriteBatch, Vector2 position, float scale, float alpha)
        {
            // Only draw if the farmer's head hash is non-null (i.e. their head texture has been generated).
            if (farmerHeadHash == -1) return;
            
            // If the target alpha is changed, re-linearly interpolate starting from the current alpha to the new
            // target alpha.
            if (Math.Abs(alpha - alphaLerpUtil.TargetValue) > 0.002f)
                alphaLerpUtil = new SmoothLerpUtil(currentAlpha, alpha, ALPHA_DELTA_TIME);
                
            int width = compassTexture.Width, height = compassTexture.Height;
            spriteBatch.Draw(
                compassTexture, position, new Rectangle(0, 0, width, height), Color.White * currentAlpha, 
                0, new Vector2(width / 2f, height / 2f), scale * Game1.options.uiScale, SpriteEffects.None, 0.8f);
        }
        
        
        
        /*********
        ** Private methods
        *********/
        /// <summary>Invoked before/after the game state is updated (~60 times per second).</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            void HandleAlphaTransition()
            {
                if (e.IsMultipleOf(4)) // Only update alpha ~15 times per second.
                    currentAlpha = alphaLerpUtil.CurrentValue;
            }

            void HandleTextureRegeneration()
            {
                if (!e.IsMultipleOf(30)) return; // Only run function every half second.
            
                // Regenerate the compass texture if the head hash has changed and is non-null. As texture generation is
                // expensive, we only want to regenerate it when the farmer changes their appearance.
                var currentHeadHash = GenerateHeadHash();
                if (currentHeadHash == farmerHeadHash || currentHeadHash == -1) return;
            
                farmerHeadHash = currentHeadHash;
                compassTexture = GenerateTexture();   
            }
            
            HandleAlphaTransition();
            HandleTextureRegeneration();
        }
        
        
        
        /// <summary>Generates a hash code representing all visible components of the farmer's head features.</summary>
        /// <returns>The head hash code.</returns>
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
        
        
        
        /// <summary>Generates the compass bubble texture so that it appears uniform under semi-transparency.</summary>
        /// <returns>The generated compass bubble texture.</returns>
        private Texture2D GenerateTexture()
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
    }

    
    
    /// <summary>
    /// A utility for smooth (i.e. eased in-out) linear interpolations over a time interval.
    /// </summary>
    public readonly struct SmoothLerpUtil
    {
        public SmoothLerpUtil(float initialValue, float targetValue, float deltaTime)
        {
            InitialValue = initialValue; 
            TargetValue  = targetValue; 
            DeltaTime    = deltaTime; 
            Stopwatch    = new Stopwatch();
            Stopwatch.Start();
        }

        public float InitialValue { get; }
        public float TargetValue { get; }
        public float CurrentValue { get 
            {
                // Linearly interpolate (w/ ease-in-out) if the target alpha value is not yet reached.
                if (Stopwatch.ElapsedMilliseconds < DeltaTime)
                {
                    var time = MathHelper.Clamp(Stopwatch.ElapsedMilliseconds / DeltaTime, 0f, 1f);
                    return StardewValley.Utility.Lerp(InitialValue, TargetValue, EaseInOut(time));
                }
                Stopwatch.Stop();
                return TargetValue;
            } 
        }
        
        private Stopwatch Stopwatch { get; }
        private float DeltaTime { get; }

        private static float EaseInOut(float t) => StardewValley.Utility.Lerp(t * t, 2 * t - (t * t), t);
    }
}