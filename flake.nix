{
  description = "Drilla Linux compiler development toolchain";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/5fa1ef6f8d1a66829571930adfa6e214caaff9a6";

  outputs =
    { nixpkgs, ... }:
    let
      system = "x86_64-linux";
      pkgs = import nixpkgs { inherit system; };
      llvm = pkgs.llvmPackages_16.llvm;
      vulkanLoader = pkgs.vulkan-loader;
    in
    {
      devShells.${system}.default = pkgs.mkShell {
        packages = with pkgs; [
          dotnet-sdk_9
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

      formatter.${system} = pkgs.nixfmt-rfc-style;
    };
}
