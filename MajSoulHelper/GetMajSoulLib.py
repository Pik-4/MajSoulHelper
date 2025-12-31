import os


pyName = os.path.basename(__file__)
pyPath = os.path.realpath(__file__)
realWorkDir = pyPath[:-len(pyName)]
unity_mono = os.listdir(realWorkDir+"LibUnityMono")
MajSoulDllPath = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\MahjongSoul\\BepInEx\\interop\\"
for dll in os.listdir(MajSoulDllPath):
    if dll.endswith('dll') and (dll not in unity_mono):
        print(f'copy /y "{MajSoulDllPath+dll}" "{realWorkDir}LibMajSoul\\{dll}"')
        os.system(f'copy /y "{MajSoulDllPath+dll}" "{realWorkDir}LibMajSoul\\{dll}"')
