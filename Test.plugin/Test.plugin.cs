namespace TestPlugin {
    public record OtherBar(string name, int number): IFoo;
    public class StaticCtor: IFoo {
        public static StaticCtor MakeFoo(string name, int number) {
            return new StaticCtor { _name = name, _number = number };
        }
        public static OtherBar MakeFoo2(string name, int number) {
            return new(name, number);
        }
        string _name = "";
        int _number = 0;
        public string name => _name;
        public int number => _number;
    }
}