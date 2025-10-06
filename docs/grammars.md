# Riddle 语言语法说明（草案）

## 程序结构

- **编译单元**：文件由三部分按顺序组成
    1) 可选 `package` 声明；
    2) 任意多个 `import`；
    3) 若干语句（变量/函数/类等）。
- **限定名**：使用 `::` 分隔（如 `foo::bar::Baz`）。
- **分号**：除代码块外，语句以 `;` 结尾。
- **空白**：空格/制表符/换行会被忽略；**当前未定义注释语法**。

**示例**
```riddle
package app::demo;

import std::io;
import math::utils;

var version: Int = 1;

fun main() {
    io::println("Riddle!");
}
```

## 语句与声明

### 代码块
- `{ ... }` 包含零个或多个语句，可用作任何需要语句的位置。

```riddle
{
    var x = 1;
    x = x + 1;
}
```

### 变量声明
- 形式：`var 标识符 [: 类型表达式]? [= 初值表达式]?;`
- 类型注解和初值均可省略；类型写作**表达式**（通常是一个名字）。

```riddle
var a;                   // 未指定类型和初值（依实现决定默认行为）
var b: Int;              // 只指定类型
var c = 42;              // 只指定初值
var d: Int = 7;          // 同时指定
```

### 函数声明
- 形式：`fun 名字(参数列表)? [-> 返回类型]? { ... }`  
  或**仅声明**：末尾 `;`，无函数体（前向声明）。
- **参数**：`name: 类型表达式`，以逗号分隔。
- **可变参数**：使用 `...`；可以单独作为整个参数列表，或置于参数列表末尾。

```riddle
fun ping();                         // 前向声明

fun add(x: Int, y: Int) -> Int {
    return x + y;
}

fun printAll(...){                  // 仅可变参数
    // ...
}

fun log(tag: Str, ...){ /* ... */ } // 固定参数后跟 ...
```

### 返回语句
- `return` 后可带返回值：`return;` / `return expr;`

### 条件与循环
```riddle
if (x > 0) return;                  // 单语句分支
if (flag) { doA(); } else doB();    // 可选 else；分支可为任意语句

while (i < n) i = i + 1;            // 循环体为语句或代码块
```

### 类声明
- 形式：`class 名字 { ... }`
- 类体是一个**代码块**，可包含变量、函数、控制语句等（语义由实现决定）。

```riddle
class Counter {
    var value: Int = 0;

    fun inc() { value = value + 1; }
    fun get() -> Int { return value; }
}
```

### 表达式语句
- 任意表达式后跟分号：

```riddle
doWork(x, y);
obj.method().chain();
```

## 表达式

> 表达式统一采用**左递归**定义；当前版本**未显式区分运算符优先级**（见后文）。

- **字面量**：`Int`、`Bool`（`true` / `false`）。
- **名字/限定名**：`qName`，例如 `math::PI`。
- **函数调用**：`callee(args...)`，其中 `callee` 本身是表达式（可调用值）。
- **成员访问**：`left . right`
  > 右侧目前按“表达式”解析（实验性）；常见写法依然是 `obj.field`、`obj.method()`
- **二元运算**：`+ - * / % == != < > <= >= =`  
  `=` 作为**赋值表达式**也属于二元运算。
- **后缀运算（实验性）**：
    - `expr ?`：意图为“空指针？”一类的后缀检查（占位，语义待定）。
    - `expr *`：意图为指针解引用/指针标记（占位，语义待定）。  
      实际含义以实现为准，建议暂不依赖。

**示例**
```riddle
a = b + 1;
sum( f(x), g(y) );
user.profile.name;
obj.method()(42);           // 可调用返回值再调用
(ns::factory()).build();
```

## 运算符结合性与优先级

- **结合性**：所有二元运算符按**左结合**处理：`a - b - c` 解析为 `(a - b) - c`。
- **优先级**：**未区分**。也就是说 `a + b * c` 与 `(a + b) * c` 解析等价。
  > 在需要常见优先级（如乘除优先于加减）的场景，请**使用括号**明确计算顺序。
- **赋值**：`=` 与其他二元运算同级；`a = b = 1` 解析为 `(a = b) = 1`，通常并不合法或会出乎意料，建议写作 `b = 1; a = b;`。

**建议**：凡是包含多种运算混合或链式成员/调用混合二元运算时，**加括号**以消除歧义。

## 词法元素

- **标识符**：`[a-zA-Z_][a-zA-Z0-9_]*`
- **整数字面量**：`0` 或不以 0 开头的十进制数。
- **关键字**：`var, fun, package, import, return, if, else, while, class`
- **符号**：`; : , = ( ) { } -> * . ? ...`
- **空白**：空格/制表/换行被忽略。**暂无注释**。

---

## 更多示例

### 1) 包与导入
```riddle
package tools::text;
import std::io;
import std::str;

fun main() {
    io::println(str::upper("hello"));
}
```

### 2) 变量与赋值（赋值作为表达式）
```riddle
var x: Int;
var y = 3;
if ((x = y + 2) == 5) {
    // ...
}
```

### 3) 函数：声明、可变参数、返回类型
```riddle
fun declOnly(a: Int);                // 仅声明

fun sumAll(...)->Int {
    // 假设运行时支持从 ... 读取参数
    return 0;
}

fun compose(f: Fn, g: Fn) -> Fn {
    return fn(x) { return f(g(x)); };
}
```

### 4) if / while 与单语句体
```riddle
if (ready) start(); else { stop(); }

var i = 0;
while (i < 10) i = i + 1;
```

### 5) 类与成员/方法
```riddle
class Point {
    var x: Int;
    var y: Int;

    fun move(dx: Int, dy: Int) {
        x = x + dx;
        y = y + dy;
    }
}

var p: Point;
p.x = 1; p.y = 2;
p.move(3, 4);
```

### 6) 链式访问与调用
```riddle
factory().create().builder.name;
(obj.method().next())(123);
```

---

## 现状与注意事项

- 成员访问右侧与两个后缀运算符（`?` / `*`）标注为**待完善**，语义可能变化。
- 由于**无运算符优先级**，请在复杂表达式中使用括号，或拆成多条语句以提升可读性与可移植性。
- 目前**不支持注释**；

