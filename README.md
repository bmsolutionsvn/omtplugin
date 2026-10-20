# Open Media Transport Plugin

omtplugin is a cross platform Open Media Transport plugin for OBS (https://obsproject.com)

## Download

https://github.com/openmediatransport/omtplugin/releases

## Features

Send and receive high quality audio/video between OBS and other OMT compatible devices over a local network!

For more information about Open Media Transport in general, see the README here:
https://github.com/openmediatransport/

### OMT Source

Receive audio/video from OMT sources on the network. The Mode dropdown picks how the picture travels:

- **OMT (Full)** sends the whole picture with VMX. It carries alpha, keeps HDR sources (Color Space dropdown),
  and uses Preview Mode for a small picture.
- **OMT HX** carries HEVC or H.264 from the source's hardware encoder, at 8-bit 4:2:0. It uses far less
  bandwidth on links that cannot carry the full picture, and it needs no setting on the receiving side.

### OMT Output

Send audio/video from the main OBS output, from Tools - OMT Output Settings. The same Mode dropdown applies:

- **OMT (Full)** sends 10bit+ when P010 or P216 is enabled in OBS settings.
- **OMT HX** codes HEVC or H.264 (HX Codec) on the GPU, or on the CPU when no hardware encoder is available.
  **HX Alpha** adds the alpha channel (BGRA output only); alpha always codes at high quality.

OMT-HX costs latency to save bandwidth: about 10-15 ms of encode/decode at 1080p60 and 15-50 Mbps. Full OMT
is visually lossless and carries the whole picture. See https://github.com/bmsolutionsvn/OMT-HX.

## Requirements

OBS 31 or higher.

OMT HX needs a hardware video encoder: AMF, NVENC, Quick Sync or Media Foundation on Windows; NVENC, or the
software libx264/libx265 encoders, on Linux. macOS offers Full OMT only for now.

## Installation

### Windows

1. Download the Windows binaries from Releases on GitHub
2. Copy `omtplugin.dll`, `libomt.dll`, `libvmx.dll`, `omthx.dll` and the FFmpeg DLLs (`av*.dll`, `sw*.dll`) into
   the following folder:

```
C:\ProgramData\obs-studio\plugins\omtplugin\bin\64bit
```

This folder will need to be created if it does not already exist.

**Note:** Replace C: if Windows has been installed to a different drive.

### MacOS

1. Download the MacOS pkg from Releases on GitHub
2. Open the package and follow the instructions to install

### Linux

The install instructions below are based on Linux Distributions using the **APT** package manager.

1. Update the package manager

```
sudo apt update
```

2. Install dotnet 8

```
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0

echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.bashrc
source ~/.bashrc
```

3. Install the build tools, FFmpeg development packages and Avahi

```
sudo apt install clang cmake ninja-build git pkg-config zlib1g-dev \
  libavcodec-dev libavformat-dev libavutil-dev libswscale-dev libswresample-dev \
  avahi-daemon libavahi-client3
```

4. Clone the repositories into the home directory. OMT-HX carries libomt, libomtnet and libvmx as submodules.

```
cd ~/
git clone --recurse-submodules https://github.com/bmsolutionsvn/OMT-HX
git clone https://github.com/bmsolutionsvn/omtplugin
```

5. Build OMT-HX and the plugin

```
cd ~/omtplugin/build
chmod 755 buildlinuxx64.sh
./buildlinuxx64.sh
```

6. Copy the plugin and libraries into the OBS plugins directory

```
sudo cp ~/omtplugin/bin/Release/net8.0/linux-x64/publish/omtplugin.so \
        ~/omtplugin/bin/Release/net8.0/linux-x64/publish/libomthx.so \
        ~/omtplugin/bin/Release/net8.0/linux-x64/publish/libomt.so \
        ~/omtplugin/bin/Release/net8.0/linux-x64/publish/libvmx.so \
        /usr/lib/x86_64-linux-gnu/obs-plugins/
```

FFmpeg comes from the distribution packages above, so the plugin and OMT-HX load the same libavcodec OBS does.

## Build

Windows: `build\buildwinx64.cmd`. Linux: `build/buildlinuxx64.sh`. Both expect an OMT-HX checkout beside this
repository (`..\OMT-HX`) and build it first: OMT-HX owns libomt, libomtnet and the HX codecs.