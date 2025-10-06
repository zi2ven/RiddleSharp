grammar Riddle;

compileUnit
    : (packageStmt)? (importStmt)* statememt*
    ;
    
statememt
    : block
    | varDecl
    | funcDecl
    | exprStmt
    | returnStmt
    | ifStmt
    | whileStmt
    | classDecl
    ;

packageStmt
    : Package name=qName Semi
    ;
    
importStmt
    : Import name=qName Semi
    ;

varDecl
    : Var name=Identifier (Colon type=expression)? (Assign value=expression)? Semi
    ;
    
funcParam
    : name=Identifier Colon type=expression
    ;    

funcParamList
    : vararg=ELLIPSIS
    | params+=funcParam (Comma params+=funcParam)* (Comma vararg=ELLIPSIS)?
    ;

funcDecl
    : Fun name=Identifier LParen funcParamList? RParen (Arrow type=expression)? ((body=block)|Semi)
    ;
    
ifStmt
    : If LParen cond=expression RParen then=statememt (Else else=statememt)?
    ;
    
whileStmt
    : While LParen cond=expression RParen body=statememt
    ;
    
block
    : LBrace statememt* RBrace
    ;    

returnStmt
    : Return (result=expression)? Semi
    ;

classDecl
    : Class name=Identifier body=block
    ;

exprStmt
    : expression Semi
    ;
    
expression
    : callee=expression LParen (args+=expression (Comma args+=expression)*)? RParen #call
    | expression QMark #nullPointer //todo
    | expression Star #pointer      //todo
    | expression Dot expression #memberAccess //todo
    | left=expression op right=expression #binaryOp
    | IntLit #integer
    | BoolLit #boolean
    | qName #symbol
    ;
    
qName
    : (Identifier Colon Colon)* Identifier
    ;   
 
op
    : '+' | '-' | '*' | '/' | '%' | '==' | '!=' | '<' | '>' | '<=' | '>=' 
    | Assign
    ;
    
Var: 'var';
Fun: 'fun';
Package: 'package' ;
Import: 'import' ;
Return: 'return' ;
If: 'if' ;
Else: 'else' ;
While: 'while';
Class: 'class';

Semi: ';';
Colon: ':';
Comma: ',';
Assign: '=';
LParen : '(' ;
RParen : ')' ;
LBrace : '{' ;
RBrace : '}' ;
Arrow: '->';
Star: '*';
Dot : '.';
QMark: '?';
ELLIPSIS: Dot Dot Dot;

BoolLit: 'true' | 'false';
IntLit: [1-9][0-9]* | '0';
Identifier: [a-zA-Z_][a-zA-Z0-9_]*;

WD: [\t\r\n ] -> skip;