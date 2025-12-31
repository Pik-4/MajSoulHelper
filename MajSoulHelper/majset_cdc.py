import string
import os

class EncDec:
    """
    模拟原始 C# 代码中 EncDec 类的行为，使用类属性来存储全局状态，
    确保字符集和映射只初始化一次。
    """
    # 类属性，存储全局状态
    charMap = None  # 存储字符到索引的映射 (字典)
    charList = None # 存储所有可加密字符的列表 (列表)
    # 字符集大小 (总共 40 个字符)
    CHAR_LIST_COUNT = 40 

    @classmethod
    def _initialize_char_data(cls):
        """
        初始化 charMap 和 charList。只应运行一次。
        """
        if cls.charMap is not None:
            return # 如果已经初始化，则直接返回

        # 1. 初始化 charList
        char_list = []
        
        # 添加 a-z (ASCII 97-122)
        char_list.extend(string.ascii_lowercase)

        # 添加 0-9 (ASCII 48-57)
        char_list.extend(string.digits)
        
        # 添加特殊字符 @, $, _, -
        char_list.extend(['@', '$', '_', '-'])
        
        cls.charList = char_list
        
        # 2. 初始化 charMap (字符到索引的映射)
        cls.charMap = {char: index for index, char in enumerate(cls.charList)}


    @classmethod
    def EncryptString(cls, str_to_encrypt: str) -> str:
        """
        加密字符串。
        加密逻辑: new_index = (original_index + l + 17) % N
        """
        
        # 确保字符集已初始化
        cls._initialize_char_data()

        result_chars = []
        char_list_count = cls.CHAR_LIST_COUNT
        KEY_OFFSET = 17 # 密钥偏移量

        # 遍历输入字符串中的每个字符及其位置索引 l
        for l, c2 in enumerate(str_to_encrypt):
            
            # 如果字符在字符集中
            if c2 in cls.charMap:
                original_index = cls.charMap[c2]
                
                # 加密公式: (原始索引 + 位置索引 + 密钥偏移) % 字符集长度
                new_index = (original_index + l + KEY_OFFSET) % char_list_count
                
                result_chars.append(cls.charList[new_index])
            else:
                # 不在字符集中的字符保持不变
                result_chars.append(c2)

        return "".join(result_chars)

    # ---
    
    @classmethod
    def DecryptString(cls, str_to_decrypt: str) -> str:
        """
        解密字符串。
        解密逻辑: new_index = (original_index - l - 17 + N * 100) % N
        """
        
        # 确保字符集已初始化
        cls._initialize_char_data()

        result_chars = []
        char_list_count = cls.CHAR_LIST_COUNT
        KEY_OFFSET = 17 # 密钥偏移量

        # 遍历输入字符串中的每个字符及其位置索引 l
        for l, c2 in enumerate(str_to_decrypt):
            
            # 如果字符在字符集中
            if c2 in cls.charMap:
                original_index = cls.charMap[c2]
                
                # 解密公式: (加密后索引 - 位置索引 - 密钥偏移 + 补偿) % 字符集长度
                # 原始代码中的 ( ... + EncDec.charList.Count * 100) 用于确保结果在取模前为正。
                # 在 Python 中，取模 (%) 运算符对负数有不同的行为，
                # 但原始 C# 逻辑是确保结果为正的常见做法。
                # Python 的 % 运算符通常会给出正确的结果，但为保持与 C# 逻辑的一致性，
                # 我们可以使用一个足够大的倍数（如 char_list_count）来补偿负值。
                # (char_list_count * 100) 只是为了确保取模前为正。
                
                new_index = (original_index - l - KEY_OFFSET + char_list_count) % char_list_count
                
                result_chars.append(cls.charList[new_index])
            else:
                # 不在字符集中的字符保持不变
                result_chars.append(c2)

        return "".join(result_chars)


if __name__ == "__main__":
    majsoul = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\MahjongSoul\\Jantama_MahjongSoul_Data\\StreamingAssets\\StandaloneWindows"
    print(majsoul)
    majset = os.listdir(majsoul)
    for n in majset:
        enc = n[:len(n)-len("_3dccd20add5b634b6ed9.majset")]
        print(n,"==>",EncDec.DecryptString(enc).replace("$@$","_"))


# # ---
# ## 示例用法

# # 原始字符串
# original_string = "hello_world_123$"

# # 1. 加密
# encrypted = EncDec.EncryptString(original_string)
# print(f"原始字符串: {original_string}")
# print(f"加密结果:   {encrypted}")

# # 2. 解密
# decrypted = EncDec.DecryptString(encrypted)
# print(f"解密结果:   {decrypted}")

# # 验证
# if decrypted == original_string:
    # print("\n✅ 加密和解密成功，结果一致！")
# else:
    # print("\n❌ 失败，解密结果与原始字符串不符。")