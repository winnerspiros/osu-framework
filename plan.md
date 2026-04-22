1. *Modify `osu.Framework.Android/AndroidGameHost.cs` to delay drawing for a few frames after the surface becomes ready.*
   - Introduce a `surfaceReadyFrames` counter in `AndroidGameHost`.
   - In `DrawFrame`, only call `base.DrawFrame()` if `AndroidGameActivity.Surface.IsSurfaceReady` is true and `surfaceReadyFrames` is at least 2.
2. *Modify `osu.Framework/Graphics/Veldrid/VeldridDevice.cs` to handle surface loss in `SwapBuffers`.*
   - Wrap `Device.SwapBuffers()` in a try-catch block.
   - Specifically catch `VeldridException`.
   - If the OS is Android and the exception message indicates that the surface has been lost, log the error and return gracefully.
   - Track the last known Android surface handle and log if it changes during `SwapBuffers`.
3. *Verify the changes.*
   - Use `read_file` to confirm that `osu.Framework.Android/AndroidGameHost.cs` and `osu.Framework/Graphics/Veldrid/VeldridDevice.cs` have been updated correctly with the intended logic.
4. *Run relevant tests.*
   - Run `dotnet test osu.Framework.Tests` to ensure that core framework functionality remains intact.
5. *Complete pre-commit steps.*
   - Ensure proper testing, verification, review, and reflection are done.
6. *Submit the changes.*
