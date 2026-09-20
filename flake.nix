{
  description = "Drilla Linux compiler development toolchain";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/5fa1ef6f8d1a66829571930adfa6e214caaff9a6";
  inputs.dotnet-nixpkgs.url = "github:NixOS/nixpkgs/20b1ddd1aa5ace70c9468305030aa4f9ef79671b";

  outputs =
    { dotnet-nixpkgs, nixpkgs, ... }:
    let
      system = "x86_64-linux";
      pkgs = import nixpkgs { inherit system; };
      dotnetPkgs = import dotnet-nixpkgs { inherit system; };
      gst = dotnetPkgs.gst_all_1;
      mediaPlugins = [
        gst.gstreamer.out
        gst.gst-plugins-base
        gst.gst-plugins-good
        gst.gst-plugins-bad
        dotnetPkgs.libnice.out
      ];
      llvm = pkgs.llvmPackages_16.llvm;
      vulkanLoader = pkgs.vulkan-loader;
    in
    {
      devShells.${system} = {
        default = pkgs.mkShell {
          packages = with pkgs; [
            dotnetPkgs.dotnet-sdk_10
            llvm
            nodejs_22
            pnpm
            shader-slang
            wabt
          ];

          shellHook = ''
            export LD_LIBRARY_PATH="${
              pkgs.lib.makeLibraryPath [
                llvm
                vulkanLoader
              ]
            }''${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
          '';
        };

        media = dotnetPkgs.mkShell {
          packages = [
            dotnetPkgs.dotnet-sdk_10
            gst.gstreamer
            dotnetPkgs.chromium
          ]
          ++ mediaPlugins;

          shellHook = ''
            export GST_PLUGIN_SYSTEM_PATH_1_0="${dotnetPkgs.lib.makeSearchPath "lib/gstreamer-1.0" mediaPlugins}"
            export LD_LIBRARY_PATH="${
              dotnetPkgs.lib.makeLibraryPath ([ dotnetPkgs.glib ] ++ mediaPlugins)
            }''${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
          '';
        };
      };

      formatter.${system} = pkgs.nixfmt-rfc-style;
    };
}
